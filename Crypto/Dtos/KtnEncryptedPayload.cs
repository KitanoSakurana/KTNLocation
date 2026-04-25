using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Crypto;

public sealed class KtnEncryptedPayload
{
    [Required]
    public string PacketHeader { get; set; } = "KTN";

    [Required]
    public string Algorithm { get; set; } = "RSA-OAEP-256+AES-CBC-256";

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Iv { get; set; } = string.Empty;

    [Required]
    public string Data { get; set; } = string.Empty;

    public long Timestamp { get; set; }
}
