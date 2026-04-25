using System.Security.Cryptography;
using System.Text;
using KTNLocation.Data;
using KTNLocation.Models.Dtos.Crypto;
using KTNLocation.Models.Entities;
using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services;

public sealed class RsaCryptoService : ICryptoService
{
    private readonly KTNLocationDbContext _dbContext;
    private readonly ServerRsaKeyStore _serverRsaKeyStore;
    private readonly KtnSecurityOptions _options;

    public RsaCryptoService(
        KTNLocationDbContext dbContext,
        ServerRsaKeyStore serverRsaKeyStore,
        IOptions<KtnSecurityOptions> options)
    {
        _dbContext = dbContext;
        _serverRsaKeyStore = serverRsaKeyStore;
        _options = options.Value;
    }

    public Task<GeneratedKeyPairResponse> GenerateRsaKeyPairAsync(int keySize)
    {
        var safeKeySize = Math.Clamp(keySize, 1024, 8192);
        using var rsa = RSA.Create(safeKeySize);

        return Task.FromResult(new GeneratedKeyPairResponse
        {
            PublicKeyPem = rsa.ExportRSAPublicKeyPem(),
            PrivateKeyPem = rsa.ExportRSAPrivateKeyPem()
        });
    }

    public async Task<string> GetServerPublicKeyPemAsync(CancellationToken cancellationToken = default)
    {
        return await _serverRsaKeyStore.GetPublicKeyPemAsync(cancellationToken);
    }

    public async Task RegisterClientPublicKeyAsync(string clientId, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("ClientId 不能为空。", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            throw new ArgumentException("PublicKeyPem 不能为空。", nameof(publicKeyPem));
        }

        ValidateRsaPublicKey(publicKeyPem);

        var normalizedClientId = clientId.Trim();
        var existing = await _dbContext.ClientPublicKeys
            .FirstOrDefaultAsync(x => x.ClientId == normalizedClientId, cancellationToken);

        if (existing is null)
        {
            var newEntity = new ClientPublicKey
            {
                ClientId = normalizedClientId,
                PublicKeyPem = publicKeyPem.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _dbContext.ClientPublicKeys.AddAsync(newEntity, cancellationToken);
        }
        else
        {
            existing.PublicKeyPem = publicKeyPem.Trim();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasClientPublicKeyAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        var normalizedClientId = clientId.Trim();
        return await _dbContext.ClientPublicKeys
            .AsNoTracking()
            .AnyAsync(x => x.ClientId == normalizedClientId, cancellationToken);
    }

    public async Task<KtnEncryptedPayload> EncryptPayloadForClientAsync(
        string clientId,
        string plainText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            plainText = "{}";
        }

        var clientPublicKey = await _dbContext.ClientPublicKeys
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .Select(x => x.PublicKeyPem)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(clientPublicKey))
        {
            throw new InvalidOperationException("客户端公钥不存在，请先注册客户端公钥。");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(clientPublicKey);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateKey();
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedData;
        using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
        {
            encryptedData = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        var encryptedAesKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);

        return new KtnEncryptedPayload
        {
            PacketHeader = _options.PacketHeaderValue,
            Algorithm = "RSA-OAEP-256+AES-CBC-256",
            Key = Convert.ToBase64String(encryptedAesKey),
            Iv = Convert.ToBase64String(aes.IV),
            Data = Convert.ToBase64String(encryptedData),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public async Task<string> DecryptPayloadWithServerPrivateKeyAsync(
        DecryptPayloadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var encryptedAesKey = Convert.FromBase64String(request.Key);
            var iv = Convert.FromBase64String(request.Iv);
            var data = Convert.FromBase64String(request.Data);

            using var privateRsa = await _serverRsaKeyStore.CreatePrivateRsaAsync(cancellationToken);
            var aesKey = privateRsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.IV = iv;

            byte[] decrypted;
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            {
                decrypted = decryptor.TransformFinalBlock(data, 0, data.Length);
            }

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("解密失败，请检查加密数据结构与密钥。", ex);
        }
    }

    private static void ValidateRsaPublicKey(string pem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("PublicKeyPem 不是有效的 RSA 公钥 PEM。", ex);
        }
    }
}
