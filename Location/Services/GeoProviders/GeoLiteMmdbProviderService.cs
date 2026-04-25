using KTNLocation.Options;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class GeoLiteMmdbProviderService : MmdbIpGeoProviderBase
{
    private readonly GeoProviderOptions _options;

    public GeoLiteMmdbProviderService(
        IOptions<GeoProviderOptions> options,
        ILogger<GeoLiteMmdbProviderService> logger)
        : base(logger)
    {
        _options = options.Value;
    }

    public override string Name => "geolite";

    protected override string DatabasePath => _options.GeoLiteCountryMmdbPath;
}
