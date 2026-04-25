using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Crypto;

public sealed class DecryptPayloadRequest
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Iv { get; set; } = string.Empty;

    [Required]
    public string Data { get; set; } = string.Empty;
}
