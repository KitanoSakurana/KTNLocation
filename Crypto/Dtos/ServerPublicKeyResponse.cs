namespace KTNLocation.Models.Dtos.Crypto;

public sealed class ServerPublicKeyResponse
{
    public string PacketHeaderName { get; set; } = "KTN";

    public string PacketHeaderValue { get; set; } = "KTN";

    public string EncryptRequestHeader { get; set; } = "X-KTN-Encrypt";

    public string ClientIdHeader { get; set; } = "X-Client-Id";

    public string ServerPublicKeyPem { get; set; } = string.Empty;
}
