namespace KTNLocation.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = true;

    public string InstanceName { get; set; } = "KTNLocation:";
}