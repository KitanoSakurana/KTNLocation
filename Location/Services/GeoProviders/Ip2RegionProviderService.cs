using System.Net;
using System.Net.Sockets;
using KTNLocation.Models.Domain;
using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class Ip2RegionProviderService : IIpGeoProviderService, IDisposable
{
    private readonly GeoProviderOptions _options;
    private readonly ILogger<Ip2RegionProviderService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Searcher? _searcherV4;
    private Searcher? _searcherV6;
    private bool _disposed;

    public Ip2RegionProviderService(IOptions<GeoProviderOptions> options, ILogger<Ip2RegionProviderService> logger)
    { _options = options.Value; _logger = logger; }

    public string Name => "ip2region";

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _searcherV4?.Dispose(); _searcherV4 = null;
            _searcherV6?.Dispose(); _searcherV6 = null;
            TryLoadSearcher(ref _searcherV4, _options.Ip2RegionV4XdbPath, "v4");
            TryLoadSearcher(ref _searcherV6, _options.Ip2RegionV6XdbPath, "v6");
        }
        finally { _semaphore.Release(); }
    }

    private void TryLoadSearcher(ref Searcher? searcher, string path, string label)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) { _logger.LogInformation("ip2region {Label} xdb not found: {Path}", label, fullPath); return; }
        searcher = new Searcher(CachePolicy.Content, fullPath);
        _logger.LogInformation("ip2region {Label} reloaded from {Path}", label, fullPath);
    }

    public async Task<LocationResolvedResult?> ResolveAsync(string ip, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ip, out var ipAddress)) return null;
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && ipAddress.IsIPv4MappedToIPv6)
            ipAddress = ipAddress.MapToIPv4();

        var searcher = await GetSearcherAsync(ipAddress.AddressFamily, cancellationToken);
        if (searcher is null) return null;

        string? region;
        try { region = searcher.Search(ipAddress); }
        catch (Exception ex) { _logger.LogWarning(ex, "ip2region lookup failed for {Ip}", ipAddress); return null; }
        if (string.IsNullOrWhiteSpace(region)) return null;

        var seg = region.Split('|', StringSplitOptions.TrimEntries);
        if (seg.Length == 0) return null;

        var country = Norm(seg.ElementAtOrDefault(0));
        var province = Norm(seg.ElementAtOrDefault(1));
        var city = Norm(seg.ElementAtOrDefault(2));

        return new LocationResolvedResult
        {
            Source = Name, Ip = ipAddress.ToString(),
            Country = string.IsNullOrWhiteSpace(country) ? "未知" : country,
            Province = province, City = city,
            County = !string.IsNullOrWhiteSpace(city) ? city : province,
            ResolvedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<Searcher?> GetSearcherAsync(AddressFamily af, CancellationToken ct)
    {
        var current = af == AddressFamily.InterNetwork ? _searcherV4 : _searcherV6;
        if (current is not null) return current;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (af == AddressFamily.InterNetwork)
            {
                if (_searcherV4 is not null) return _searcherV4;
                var p = ResolvePath(_options.Ip2RegionV4XdbPath);
                if (!File.Exists(p)) { _logger.LogInformation("ip2region v4 not found: {Path}", p); return null; }
                _searcherV4 = new Searcher(CachePolicy.Content, p);
                return _searcherV4;
            }
            if (_searcherV6 is not null) return _searcherV6;
            var p6 = ResolvePath(_options.Ip2RegionV6XdbPath);
            if (!File.Exists(p6)) { _logger.LogInformation("ip2region v6 not found: {Path}", p6); return null; }
            _searcherV6 = new Searcher(CachePolicy.Content, p6);
            return _searcherV6;
        }
        finally { _semaphore.Release(); }
    }

    private static string ResolvePath(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(path, Directory.GetCurrentDirectory());
    private static string Norm(string? v) => v is null or "0" ? string.Empty : v.Trim();

    public void Dispose()
    {
        if (_disposed) return;
        _searcherV4?.Dispose(); _searcherV6?.Dispose(); _semaphore.Dispose();
        _disposed = true;
    }
}
