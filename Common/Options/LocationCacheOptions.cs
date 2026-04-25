namespace KTNLocation.Options;

public sealed class LocationCacheOptions
{
    public const string SectionName = "Cache";

    public int DefaultTtlSeconds { get; set; } = 300;

    public int IpTtlSeconds { get; set; } = 900;

    public int GpsTtlSeconds { get; set; } = 300;
}
