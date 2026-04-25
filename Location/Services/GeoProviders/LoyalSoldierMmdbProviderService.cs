using KTNLocation.Options;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services.GeoProviders;

public sealed class LoyalSoldierMmdbProviderService : MmdbIpGeoProviderBase
{
    private readonly GeoProviderOptions _options;

    public LoyalSoldierMmdbProviderService(
        IOptions<GeoProviderOptions> options,
        ILogger<LoyalSoldierMmdbProviderService> logger)
        : base(logger)
    {
        _options = options.Value;
    }

    public override string Name => "loyalsoldier";

    protected override string DatabasePath => _options.LoyalSoldierCountryMmdbPath;
}
