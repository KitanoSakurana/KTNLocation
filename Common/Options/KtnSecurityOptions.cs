namespace KTNLocation.Options;

public sealed class KtnSecurityOptions
{
    public const string SectionName = "KtnSecurity";

    public bool Enabled { get; set; } = false;

    public string PacketHeaderName { get; set; } = "KTN";

    public string PacketHeaderValue { get; set; } = "KTN";

    public string EncryptRequestHeader { get; set; } = "X-KTN-Encrypt";

    public string ClientIdHeader { get; set; } = "X-Client-Id";

    public int RsaKeySize { get; set; } = 2048;

    public string ServerPrivateKeyPath { get; set; } = "KtnSecurity/server_private.pem";

    public string ServerPublicKeyPath { get; set; } = "KtnSecurity/server_public.pem";
}
