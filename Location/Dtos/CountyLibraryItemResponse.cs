namespace KTNLocation.Models.Dtos.Location;

public sealed class CountyLibraryItemResponse
{
    public long Id { get; set; }

    public string Country { get; set; } = "中国";

    public string Province { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string County { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
