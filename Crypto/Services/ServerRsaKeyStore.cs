using System.Security.Cryptography;
using KTNLocation.Options;
using Microsoft.Extensions.Options;

namespace KTNLocation.Services;

public sealed class ServerRsaKeyStore
{
    private readonly KtnSecurityOptions _options;
    private readonly ILogger<ServerRsaKeyStore> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private bool _initialized;
    private string _privateKeyPem = string.Empty;
    private string _publicKeyPem = string.Empty;

    public ServerRsaKeyStore(IOptions<KtnSecurityOptions> options, ILogger<ServerRsaKeyStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetPublicKeyPemAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _publicKeyPem;
    }

    public async Task<RSA> CreatePrivateRsaAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKeyPem);
        return rsa;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var privatePath = ResolvePath(_options.ServerPrivateKeyPath);
            var publicPath = ResolvePath(_options.ServerPublicKeyPath);

            EnsureDirectory(privatePath);
            EnsureDirectory(publicPath);

            var hasPrivate = File.Exists(privatePath);
            var hasPublic = File.Exists(publicPath);

            if (hasPrivate && hasPublic)
            {
                _privateKeyPem = await File.ReadAllTextAsync(privatePath, cancellationToken);
                _publicKeyPem = await File.ReadAllTextAsync(publicPath, cancellationToken);

                if (!IsValidKeyPair(_privateKeyPem, _publicKeyPem))
                {
                    _logger.LogWarning(
                        "Existing PEM files are invalid or mismatched. Regenerating keys. Private={PrivatePath}; Public={PublicPath}",
                        privatePath,
                        publicPath);

                    await GenerateAndSaveKeyPairAsync(privatePath, publicPath, cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "Loaded server RSA PEM key pair. Private={PrivatePath}; Public={PublicPath}",
                        privatePath,
                        publicPath);
                }
            }
            else
            {
                if (hasPrivate || hasPublic)
                {
                    _logger.LogWarning(
                        "Detected partial PEM key files. Regenerating complete key pair. PrivateExists={HasPrivate}; PublicExists={HasPublic}",
                        hasPrivate,
                        hasPublic);
                }

                await GenerateAndSaveKeyPairAsync(privatePath, publicPath, cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task GenerateAndSaveKeyPairAsync(string privatePath, string publicPath, CancellationToken cancellationToken)
    {
        var keySize = Math.Clamp(_options.RsaKeySize, 1024, 8192);
        using var rsa = RSA.Create(keySize);

        _privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        _publicKeyPem = rsa.ExportRSAPublicKeyPem();

        await File.WriteAllTextAsync(privatePath, _privateKeyPem, cancellationToken);
        await File.WriteAllTextAsync(publicPath, _publicKeyPem, cancellationToken);

        _logger.LogInformation(
            "Generated server RSA PEM key pair. KeySize={KeySize}; Private={PrivatePath}; Public={PublicPath}",
            keySize,
            privatePath,
            publicPath);
    }

    private static bool IsValidKeyPair(string privatePem, string publicPem)
    {
        try
        {
            using var privateRsa = RSA.Create();
            privateRsa.ImportFromPem(privatePem);

            using var publicRsa = RSA.Create();
            publicRsa.ImportFromPem(publicPem);

            var probe = RandomNumberGenerator.GetBytes(32);
            var encrypted = publicRsa.Encrypt(probe, RSAEncryptionPadding.OaepSHA256);
            var decrypted = privateRsa.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256);
            return probe.AsSpan().SequenceEqual(decrypted);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, Directory.GetCurrentDirectory());
    }
}
