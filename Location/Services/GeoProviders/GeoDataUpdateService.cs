using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class GeoDataUpdateService : BackgroundService
{
    private readonly GeoProviderOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEnumerable<IIpGeoProviderService> _providers;
    private readonly ILogger<GeoDataUpdateService> _logger;

    public GeoDataUpdateService(
        IOptions<GeoProviderOptions> options,
        IHttpClientFactory httpClientFactory,
        IEnumerable<IIpGeoProviderService> providers,
        ILogger<GeoDataUpdateService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _providers = providers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 首次启动延迟 30 秒，让应用完成初始化
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var interval = TimeSpan.FromHours(Math.Max(_options.UpdateIntervalHours, 1));
        using var timer = new PeriodicTimer(interval);

        do
        {
            await RunUpdateCycleAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunUpdateCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始检查 GeoIP 数据库更新...");

        var tasks = new List<(string path, string url, string provider)>
        {
            (_options.Ip2RegionV4XdbPath, _options.Ip2RegionV4XdbUrl, "ip2region"),
            (_options.Ip2RegionV6XdbPath, _options.Ip2RegionV6XdbUrl, "ip2region"),
            (_options.GeoLiteCountryMmdbPath, _options.GeoLiteCountryMmdbUrl, "geolite"),
            (_options.LoyalSoldierCountryMmdbPath, _options.LoyalSoldierCountryMmdbUrl, "loyalsoldier"),
            (_options.GeoIP2CnMmdbPath, _options.GeoIP2CnMmdbUrl, "geoip2cn"),
        };

        var updatedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, url, provider) in tasks)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(url))
                continue;

            try
            {
                if (await TryUpdateFileAsync(path, url, cancellationToken))
                    updatedProviders.Add(provider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "更新 {Url} 失败", url);
            }
        }

        foreach (var providerName in updatedProviders)
        {
            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
            if (provider is not null)
            {
                try
                {
                    await provider.ReloadAsync(cancellationToken);
                    _logger.LogInformation("Provider {Provider} 已重新加载", providerName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} 重新加载失败", providerName);
                }
            }
        }

        _logger.LogInformation("GeoIP 数据库更新检查完成，更新了 {Count} 个数据源", updatedProviders.Count);
    }

    private async Task<bool> TryUpdateFileAsync(string targetPath, string sourceUrl, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(targetPath);
        var client = _httpClientFactory.CreateClient(nameof(GeoDataUpdateService));

        using var headReq = new HttpRequestMessage(HttpMethod.Head, sourceUrl);
        using var headResp = await client.SendAsync(headReq, cancellationToken);

        if (!headResp.IsSuccessStatusCode)
            return false;

        var remoteLength = headResp.Content.Headers.ContentLength;
        var remoteLastModified = headResp.Content.Headers.LastModified;

        if (File.Exists(fullPath))
        {
            var localInfo = new FileInfo(fullPath);
            if (remoteLength.HasValue && localInfo.Length == remoteLength.Value)
                return false;
            if (remoteLastModified.HasValue && localInfo.LastWriteTimeUtc >= remoteLastModified.Value.UtcDateTime)
                return false;
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = fullPath + ".tmp";
        try
        {
            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var dst = File.Create(tmpPath);
            await src.CopyToAsync(dst, cancellationToken);
        }
        catch
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw;
        }

        File.Move(tmpPath, fullPath, overwrite: true);
        _logger.LogInformation("已更新数据文件: {Path}", fullPath);
        return true;
    }

    private static string ResolvePath(string path)
        => Path.IsPathRooted(path) ? path : Path.GetFullPath(path, Directory.GetCurrentDirectory());
}
