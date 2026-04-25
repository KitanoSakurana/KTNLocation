using System.Net;
using KTNLocation.Models.Domain;
using KTNLocation.Services.Interfaces;
using MaxMind.Db;

namespace KTNLocation.Services.GeoProviders;

public abstract class MmdbIpGeoProviderBase : IIpGeoProviderService, IDisposable
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Reader? _reader;
    private bool _disposed;

    protected MmdbIpGeoProviderBase(ILogger logger) => _logger = logger;

    public abstract string Name { get; }
    protected abstract string DatabasePath { get; }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _reader?.Dispose();
            _reader = null;
            var fullPath = ResolvePath(DatabasePath);
            if (File.Exists(fullPath))
            {
                _reader = new Reader(fullPath);
                _logger.LogInformation("MMDB provider {Provider} reloaded from {Path}", Name, fullPath);
            }
        }
        finally { _semaphore.Release(); }
    }

    public async Task<LocationResolvedResult?> ResolveAsync(string ip, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ip, out var ipAddress)) return null;
        var reader = await GetReaderAsync(cancellationToken);
        if (reader is null) return null;

        MmdbRecordModel? record;
        try { record = reader.Find<MmdbRecordModel>(ipAddress); }
        catch (Exception ex) { _logger.LogWarning(ex, "MMDB lookup failed in {Provider}.", Name); return null; }
        if (record is null) return null;

        var country = GetBestName(record.Country) ?? GetBestName(record.RegisteredCountry)
                      ?? record.Country?.IsoCode ?? record.RegisteredCountry?.IsoCode ?? string.Empty;
        var firstSub = record.Subdivisions?.FirstOrDefault();
        var province = GetBestName(firstSub) ?? string.Empty;
        var city = GetBestName(record.City) ?? string.Empty;
        var county = !string.IsNullOrWhiteSpace(city) ? city : province;

        if (string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(province) && string.IsNullOrWhiteSpace(city))
            return null;

        return new LocationResolvedResult
        {
            Source = Name, Ip = ipAddress.ToString(),
            Country = string.IsNullOrWhiteSpace(country) ? "未知" : country,
            Province = province, City = city, County = county,
            Latitude = record.Location?.Latitude, Longitude = record.Location?.Longitude,
            ResolvedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<Reader?> GetReaderAsync(CancellationToken cancellationToken)
    {
        if (_reader is not null) return _reader;
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_reader is not null) return _reader;
            var fullPath = ResolvePath(DatabasePath);
            if (!File.Exists(fullPath)) { _logger.LogInformation("MMDB not found for {Provider}: {Path}", Name, fullPath); return null; }
            _reader = new Reader(fullPath);
            return _reader;
        }
        finally { _semaphore.Release(); }
    }

    private static string? GetBestName(MmdbNamedNode? node)
    {
        if (node?.Names is null || node.Names.Count == 0) return null;
        if (node.Names.TryGetValue("zh-CN", out var zhCn) && !string.IsNullOrWhiteSpace(zhCn)) return zhCn;
        if (node.Names.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) return en;
        return node.Names.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string ResolvePath(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(path, Directory.GetCurrentDirectory());

    public void Dispose()
    {
        if (_disposed) return;
        _reader?.Dispose();
        _semaphore.Dispose();
        _disposed = true;
    }
}
