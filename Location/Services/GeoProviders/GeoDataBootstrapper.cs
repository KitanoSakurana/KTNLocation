using KTNLocation.Options;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class GeoDataBootstrapper
{
    private readonly GeoProviderOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeoDataBootstrapper> _logger;

    public GeoDataBootstrapper(IOptions<GeoProviderOptions> options, IHttpClientFactory httpClientFactory, ILogger<GeoDataBootstrapper> logger)
    { _options = options.Value; _httpClientFactory = httpClientFactory; _logger = logger; }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.AutoDownloadOnStartup) { _logger.LogInformation("Geo data auto download disabled."); return; }

        var downloads = new[]
        {
            (_options.Ip2RegionV4XdbPath, _options.Ip2RegionV4XdbUrl),
            (_options.Ip2RegionV6XdbPath, _options.Ip2RegionV6XdbUrl),
            (_options.GeoLiteCountryMmdbPath, _options.GeoLiteCountryMmdbUrl),
            (_options.LoyalSoldierCountryMmdbPath, _options.LoyalSoldierCountryMmdbUrl),
            (_options.GeoIP2CnMmdbPath, _options.GeoIP2CnMmdbUrl),
        };

        await Task.WhenAll(downloads.Select(d => EnsureDataFileAsync(d.Item1, d.Item2, cancellationToken)));
    }

    private async Task EnsureDataFileAsync(string targetPath, string sourceUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(sourceUrl)) return;
        var fullPath = ResolvePath(targetPath);
        if (File.Exists(fullPath)) return;

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(GeoDataBootstrapper));
            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var dst = File.Create(fullPath);
            await src.CopyToAsync(dst, cancellationToken);
            _logger.LogInformation("Geo data downloaded: {Path}", fullPath);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to download from {Url}", sourceUrl); }
    }

    private static string ResolvePath(string p) => Path.IsPathRooted(p) ? p : Path.GetFullPath(p, Directory.GetCurrentDirectory());
}
