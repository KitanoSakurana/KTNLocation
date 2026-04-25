using System.ComponentModel.DataAnnotations;

namespace KTNLocation.Models.Dtos.Crypto;

public sealed class GenerateKeyPairRequest
{
    [Range(1024, 8192)]
    public int KeySize { get; set; } = 2048;
}
