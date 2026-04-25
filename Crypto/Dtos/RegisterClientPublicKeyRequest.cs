using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Crypto;

public sealed class RegisterClientPublicKeyRequest
{
    [Required]
    [MaxLength(64)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string PublicKeyPem { get; set; } = string.Empty;
}
