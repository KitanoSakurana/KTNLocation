using KTNLocation.Models.Dtos.Crypto;

namespace KTNLocation.Services.Interfaces;

public interface ICryptoService
{
    Task<GeneratedKeyPairResponse> GenerateRsaKeyPairAsync(int keySize);

    Task<string> GetServerPublicKeyPemAsync(CancellationToken cancellationToken = default);

    Task RegisterClientPublicKeyAsync(string clientId, string publicKeyPem, CancellationToken cancellationToken = default);

    Task<bool> HasClientPublicKeyAsync(string clientId, CancellationToken cancellationToken = default);

    Task<KtnEncryptedPayload> EncryptPayloadForClientAsync(
        string clientId,
        string plainText,
        CancellationToken cancellationToken = default);

    Task<string> DecryptPayloadWithServerPrivateKeyAsync(
        DecryptPayloadRequest request,
        CancellationToken cancellationToken = default);
}
