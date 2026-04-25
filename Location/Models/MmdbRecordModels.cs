using MaxMind.Db;

namespace KTNLocation.Models.Domain;

public sealed class MmdbRecordModel
{
    [MapKey("continent")]
    public MmdbNamedNode? Continent { get; init; }

    [MapKey("country")]
    public MmdbNamedNode? Country { get; init; }

    [MapKey("registered_country")]
    public MmdbNamedNode? RegisteredCountry { get; init; }

    [MapKey("city")]
    public MmdbNamedNode? City { get; init; }

    [MapKey("subdivisions")]
    public List<MmdbNamedNode>? Subdivisions { get; init; }

    [MapKey("location")]
    public MmdbLocationNode? Location { get; init; }
}

public sealed class MmdbNamedNode
{
    [MapKey("iso_code")]
    public string? IsoCode { get; init; }

    [MapKey("names")]
    public Dictionary<string, string>? Names { get; init; }
}

public sealed class MmdbLocationNode
{
    [MapKey("latitude")]
    public double? Latitude { get; init; }

    [MapKey("longitude")]
    public double? Longitude { get; init; }
}
