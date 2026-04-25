using KTNLocation.Data;
using KTNLocation.Helpers;
using KTNLocation.Models.Common;
using KTNLocation.Models.Domain;
using KTNLocation.Models.Entities;
using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services;

public sealed class LocationService : ILocationService
{
    private readonly KTNLocationDbContext _dbContext;
    private readonly IRedisCacheService _cache;
    private readonly LocationCacheOptions _cacheOptions;
    private readonly ILogger<LocationService> _logger;
    private readonly List<IIpGeoProviderService> _providerOrderedList;
    private readonly Dictionary<string, IIpGeoProviderService> _providerMap;

    private static IReadOnlyList<CountyLocation>? _countyCache;
    private static readonly SemaphoreSlim CountyCacheLock = new(1, 1);

    public LocationService(
        KTNLocationDbContext dbContext, IRedisCacheService cache,
        IEnumerable<IIpGeoProviderService> providers,
        IOptions<LocationCacheOptions> cacheOptions,
        IOptions<GeoProviderOptions> providerOptions,
        ILogger<LocationService> logger)
    {
        _dbContext = dbContext; _cache = cache;
        _cacheOptions = cacheOptions.Value; _logger = logger;

        var order = providerOptions.Value.ProviderOrder
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        var list = providers.ToList();
        _providerMap = list.GroupBy(x => x.Name.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        _providerOrderedList = BuildProviderOrder(list, order);
    }

    public IReadOnlyList<string> GetProviderNames() => _providerMap.Keys.OrderBy(x => x).ToArray();

    public Task<LocationResolvedResult?> ResolveByIpAsync(string ip, CancellationToken ct = default)
        => ResolveByIpAsync(ip, null, ct);

    public async Task<LocationResolvedResult?> ResolveByIpAsync(string ip, string? provider, CancellationToken ct = default)
    {
        if (!IpAddressHelper.TryToIPv4Number(ip, out var ipNum, out var nip)) return null;
        var pk = string.IsNullOrWhiteSpace(provider) ? "auto" : provider.Trim().ToLowerInvariant();
        var ck = $"loc:ip:{pk}:{nip}";
        var cached = await _cache.GetAsync<LocationResolvedResult>(ck, ct);
        if (cached is not null) return cached;

        var ext = await ResolveByExternalAsync(nip, pk, ct);
        if (ext is not null) { await _cache.SetAsync(ck, ext, TimeSpan.FromSeconds(_cacheOptions.IpTtlSeconds), ct); return ext; }
        if (pk != "auto") return null;

        var matched = await _dbContext.IpRangeLocations.AsNoTracking()
            .Where(x => x.StartIpNumber <= ipNum && x.EndIpNumber >= ipNum)
            .OrderBy(x => x.EndIpNumber - x.StartIpNumber)
            .FirstOrDefaultAsync(ct);
        if (matched is null) return null;

        var result = new LocationResolvedResult
        {
            Source = "sqlite-ip", Ip = nip,
            Country = matched.Country, Province = matched.Province, City = matched.City, County = matched.County,
            Latitude = matched.Latitude, Longitude = matched.Longitude, ResolvedAt = DateTimeOffset.UtcNow
        };
        await _cache.SetAsync(ck, result, TimeSpan.FromSeconds(_cacheOptions.IpTtlSeconds), ct);
        return result;
    }

    public Task<LocationResolvedResult?> ResolveByProviderAsync(string provider, string ip, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(provider) ? Task.FromResult<LocationResolvedResult?>(null) : ResolveByIpAsync(ip, provider, ct);

    public async Task<LocationResolvedResult?> ResolveByGpsAsync(double latitude, double longitude, string? crs = null, CancellationToken ct = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;

        // 坐标系转换：如果客户端发送 GCJ-02（高德/腾讯），转为 WGS-84
        if (string.Equals(crs, "gcj02", StringComparison.OrdinalIgnoreCase))
        {
            var (wLat, wLon) = CoordinateConverter.Gcj02ToWgs84(latitude, longitude);
            latitude = wLat; longitude = wLon;
        }

        var ck = $"loc:gps:{latitude:F4}:{longitude:F4}";
        var cached = await _cache.GetAsync<LocationResolvedResult>(ck, ct);
        if (cached is not null) return cached;

        var counties = await GetCountyCacheAsync(ct);
        if (counties.Count == 0) return null;

        var nearest = counties
            .Select(x => new { County = x, Dist = GeoDistanceHelper.HaversineInKm(latitude, longitude, x.Latitude, x.Longitude) })
            .OrderBy(x => x.Dist).First();

        var result = new LocationResolvedResult
        {
            Source = "gps", Country = nearest.County.Country, Province = nearest.County.Province,
            City = nearest.County.City, County = nearest.County.County,
            Latitude = latitude, Longitude = longitude,
            DistanceKm = Math.Round(nearest.Dist, 4), ResolvedAt = DateTimeOffset.UtcNow
        };
        await _cache.SetAsync(ck, result, TimeSpan.FromSeconds(_cacheOptions.GpsTtlSeconds), ct);
        return result;
    }

    public async Task<LocationResolvedResult?> ResolveAsync(
        string? ip, double? lat, double? lon, string? fallbackIp, string? provider, string? crs = null, CancellationToken ct = default)
    {
        if (lat.HasValue && lon.HasValue)
        {
            var byGps = await ResolveByGpsAsync(lat.Value, lon.Value, crs, ct);
            if (byGps is not null) return byGps;
        }
        var rip = string.IsNullOrWhiteSpace(ip) ? fallbackIp : ip;
        return !string.IsNullOrWhiteSpace(rip) ? await ResolveByIpAsync(rip, provider, ct) : null;
    }

    public async Task<PagedResult<CountyLocation>> QueryCountyLibraryAsync(string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _dbContext.CountyLocations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(x => x.Province.Contains(kw) || x.City.Contains(kw) || x.County.Contains(kw));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Province).ThenBy(x => x.City).ThenBy(x => x.County)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<CountyLocation> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<IpRangeLocation>> QueryIpLibraryAsync(string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _dbContext.IpRangeLocations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(x => x.Province.Contains(kw) || x.City.Contains(kw) || x.County.Contains(kw) || x.StartIp.Contains(kw) || x.EndIp.Contains(kw));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.StartIpNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<IpRangeLocation> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<CountyLocation> AddCountyAsync(CountyLocation input, CancellationToken ct = default)
    {
        input.Country = string.IsNullOrWhiteSpace(input.Country) ? "中国" : input.Country.Trim();
        input.Province = input.Province.Trim(); input.City = input.City.Trim(); input.County = input.County.Trim();
        input.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.CountyLocations.AddAsync(input, ct);
        await _dbContext.SaveChangesAsync(ct);
        _countyCache = null; // invalidate cache
        return input;
    }

    public async Task<IpRangeLocation> AddIpRangeAsync(IpRangeLocation input, CancellationToken ct = default)
    {
        if (!IpAddressHelper.TryToIPv4Number(input.StartIp, out var sn, out var si)
            || !IpAddressHelper.TryToIPv4Number(input.EndIp, out var en, out var ei))
            throw new ArgumentException("IP 段格式无效，仅支持 IPv4。");
        if (sn > en) throw new ArgumentException("StartIp 不能大于 EndIp。");
        input.StartIp = si; input.EndIp = ei; input.StartIpNumber = sn; input.EndIpNumber = en;
        input.Country = string.IsNullOrWhiteSpace(input.Country) ? "中国" : input.Country.Trim();
        input.Province = input.Province.Trim(); input.City = input.City.Trim(); input.County = input.County.Trim();
        input.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.IpRangeLocations.AddAsync(input, ct);
        await _dbContext.SaveChangesAsync(ct);
        return input;
    }

    private async Task<IReadOnlyList<CountyLocation>> GetCountyCacheAsync(CancellationToken ct)
    {
        if (_countyCache is not null) return _countyCache;
        await CountyCacheLock.WaitAsync(ct);
        try
        {
            _countyCache ??= await _dbContext.CountyLocations.AsNoTracking().ToListAsync(ct);
            return _countyCache;
        }
        finally { CountyCacheLock.Release(); }
    }

    private static List<IIpGeoProviderService> BuildProviderOrder(IReadOnlyList<IIpGeoProviderService> providers, IReadOnlyList<string> order)
    {
        var map = providers.ToDictionary(x => x.Name.Trim().ToLowerInvariant(), x => x);
        var ordered = new List<IIpGeoProviderService>();
        foreach (var name in order) { if (map.Remove(name, out var p)) ordered.Add(p); }
        ordered.AddRange(map.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private async Task<LocationResolvedResult?> ResolveByExternalAsync(string nip, string pk, CancellationToken ct)
    {
        if (pk != "auto")
        {
            if (!_providerMap.TryGetValue(pk, out var sp)) { _logger.LogInformation("Provider {P} not registered.", pk); return null; }
            return await sp.ResolveAsync(nip, ct);
        }
        foreach (var p in _providerOrderedList) { var r = await p.ResolveAsync(nip, ct); if (r is not null) return r; }
        return null;
    }
}
