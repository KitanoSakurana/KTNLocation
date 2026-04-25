namespace KTNLocation.Models.Domain;

public sealed class LocationResolvedResult
{
    public string Source { get; set; } = string.Empty;

    public string? Ip { get; set; }

    public string Country { get; set; } = "中国";

    public string Province { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string County { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? DistanceKm { get; set; }

    public DateTimeOffset ResolvedAt { get; set; } = DateTimeOffset.UtcNow;
}
