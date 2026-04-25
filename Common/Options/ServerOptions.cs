namespace KTNLocation.Options;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string Address { get; set; } = "localhost";

    public int HttpPort { get; set; } = 5186;

    public bool EnableHttps { get; set; } = false;

    public int HttpsPort { get; set; } = 7044;

    public string? HttpsCertificatePath { get; set; }

    public string? HttpsPrivateKeyPath { get; set; }

    public string? HttpsCertificatePassword { get; set; }
}