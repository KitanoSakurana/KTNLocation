using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Entities;

public sealed class ClientPublicKey
{
    [Key]
    [MaxLength(64)]
    public string ClientId { get; set; } = string.Empty;

    public string PublicKeyPem { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
