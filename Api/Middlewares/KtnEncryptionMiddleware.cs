using System.Text;
using System.Text.Json;
using KTNLocation.Models.Common;
using KTNLocation.Options;
using KTNLocation.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace KTNLocation.Middlewares;

public sealed class KtnEncryptionMiddleware
{
    private readonly RequestDelegate _next;

    public KtnEncryptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICryptoService cryptoService,
        IOptions<KtnSecurityOptions> options,
        ILogger<KtnEncryptionMiddleware> logger)
    {
        var security = options.Value;
        var shouldEncrypt = IsTruthy(context.Request.Headers[security.EncryptRequestHeader]);

        if (!shouldEncrypt || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var clientId = context.Request.Headers[security.ClientIdHeader].ToString().Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                $"请求加密响应时必须提供请求头 {security.ClientIdHeader}。");
            return;
        }

        if (!await cryptoService.HasClientPublicKeyAsync(clientId, context.RequestAborted))
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                $"客户端 {clientId} 的公钥不存在，请先调用 /api/crypto/client-public-key 注册。",
                security);
            return;
        }

        var originalBodyStream = context.Response.Body;
        await using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            if (context.Response.HasStarted)
            {
                return;
            }

            memoryStream.Position = 0;
            var responseText = await new StreamReader(memoryStream).ReadToEndAsync();

            context.Response.Body = originalBodyStream;

            if (context.Response.StatusCode == StatusCodes.Status204NoContent
                || string.IsNullOrWhiteSpace(responseText)
                || !IsJsonResponse(context.Response.ContentType, responseText))
            {
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    var plainBytes = Encoding.UTF8.GetBytes(responseText);
                    context.Response.ContentLength = plainBytes.Length;
                    await context.Response.Body.WriteAsync(plainBytes, context.RequestAborted);
                }

                return;
            }

            var encryptedPayload = await cryptoService.EncryptPayloadForClientAsync(clientId, responseText, context.RequestAborted);
            context.Response.Headers[security.PacketHeaderName] = security.PacketHeaderValue;
            context.Response.ContentType = "application/json; charset=utf-8";

            var encryptedJson = JsonSerializer.Serialize(encryptedPayload);
            var encryptedBytes = Encoding.UTF8.GetBytes(encryptedJson);
            context.Response.ContentLength = encryptedBytes.Length;
            await context.Response.Body.WriteAsync(encryptedBytes, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KTN encryption middleware failed.");

            context.Response.Body = originalBodyStream;
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "响应加密失败，请检查服务器日志。",
                security);
        }
    }

    private static bool IsTruthy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJsonResponse(string? contentType, string responseText)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = responseText.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message,
        KtnSecurityOptions? securityOptions = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        if (securityOptions is not null)
        {
            context.Response.Headers[securityOptions.PacketHeaderName] = securityOptions.PacketHeaderValue;
        }

        var payload = ApiResponse<object>.Fail(message);
        await context.Response.WriteAsJsonAsync(payload);
    }
}
