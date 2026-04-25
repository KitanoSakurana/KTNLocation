using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Location;

public sealed class LocationResolveRequest
{
    [MaxLength(64)]
    public string? Ip { get; set; }

    [MaxLength(32)]
    public string? Provider { get; set; }

    [MaxLength(10)]
    public string? Crs { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }
}
