using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Location;

public sealed class CountyLocationCreateRequest
{
    [Required]
    [MaxLength(64)]
    public string Province { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string County { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}
