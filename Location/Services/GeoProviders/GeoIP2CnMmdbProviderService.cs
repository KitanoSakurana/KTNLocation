using KTNLocation.Options;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class GeoIP2CnMmdbProviderService : MmdbIpGeoProviderBase
{
    private readonly GeoProviderOptions _options;

    public GeoIP2CnMmdbProviderService(
        IOptions<GeoProviderOptions> options,
        ILogger<GeoIP2CnMmdbProviderService> logger)
        : base(logger)
    {
        _options = options.Value;
    }

    public override string Name => "geoip2cn";

    protected override string DatabasePath => _options.GeoIP2CnMmdbPath;
}
