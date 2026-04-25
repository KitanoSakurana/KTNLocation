using KTNLocation.Models.Common;
using KTNLocation.Models.Dtos.Crypto;
using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KTNLocation.Controllers;

[ApiController]
[Route("api/crypto")]
public sealed class CryptoController : ControllerBase
{
    private readonly ICryptoService _cryptoService;
    private readonly KtnSecurityOptions _securityOptions;

    public CryptoController(ICryptoService cryptoService, IOptions<KtnSecurityOptions> securityOptions)
    {
        _cryptoService = cryptoService;
        _securityOptions = securityOptions.Value;
    }

    [HttpGet("server-public-key")]
    public async Task<ActionResult<ApiResponse<ServerPublicKeyResponse>>> GetServerPublicKey(
        CancellationToken cancellationToken)
    {
        var publicKeyPem = await _cryptoService.GetServerPublicKeyPemAsync(cancellationToken);
        var response = new ServerPublicKeyResponse
        {
            PacketHeaderName = _securityOptions.PacketHeaderName,
            PacketHeaderValue = _securityOptions.PacketHeaderValue,
            EncryptRequestHeader = _securityOptions.EncryptRequestHeader,
            ClientIdHeader = _securityOptions.ClientIdHeader,
            ServerPublicKeyPem = publicKeyPem
        };

        return Ok(ApiResponse<ServerPublicKeyResponse>.Ok(response));
    }

    [HttpPost("client-public-key")]
    public async Task<ActionResult<ApiResponse<object>>> RegisterClientPublicKey(
        [FromBody] RegisterClientPublicKeyRequest request,
        CancellationToken cancellationToken)
    {
        await _cryptoService.RegisterClientPublicKeyAsync(request.ClientId, request.PublicKeyPem, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, "客户端公钥已注册。"));
    }

    [HttpPost("key-pair/generate")]
    public async Task<ActionResult<ApiResponse<GeneratedKeyPairResponse>>> GenerateKeyPair(
        [FromBody] GenerateKeyPairRequest? request,
        CancellationToken cancellationToken)
    {
        var keySize = request?.KeySize ?? _securityOptions.RsaKeySize;
        var result = await _cryptoService.GenerateRsaKeyPairAsync(keySize);
        return Ok(ApiResponse<GeneratedKeyPairResponse>.Ok(result));
    }

    [HttpPost("decrypt-with-server")]
    public async Task<ActionResult<ApiResponse<DecryptedPayloadResponse>>> DecryptWithServer(
        [FromBody] DecryptPayloadRequest request,
        CancellationToken cancellationToken)
    {
        var plainText = await _cryptoService.DecryptPayloadWithServerPrivateKeyAsync(request, cancellationToken);
        return Ok(ApiResponse<DecryptedPayloadResponse>.Ok(new DecryptedPayloadResponse
        {
            PlainText = plainText
        }));
    }
}
