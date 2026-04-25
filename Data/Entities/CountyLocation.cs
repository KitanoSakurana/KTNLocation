using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Entities;

public sealed class CountyLocation
{
    public long Id { get; set; }

    [MaxLength(64)]
    public string Country { get; set; } = "中国";

    [MaxLength(64)]
    public string Province { get; set; } = string.Empty;

    [MaxLength(64)]
    public string City { get; set; } = string.Empty;

    [MaxLength(64)]
    public string County { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
