using System.Text.Json;
using KTNLocation.Data;
using KTNLocation.Middlewares;
using KTNLocation.Options;
using KTNLocation.Services;
using KTNLocation.Services.GeoProviders;
using KTNLocation.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

var serverOptions = builder.Configuration.GetSection(ServerOptions.SectionName).Get<ServerOptions>() ?? new ServerOptions();
var redisOptions = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
var listenUrls = BuildListenUrls(serverOptions);
builder.WebHost.UseUrls(listenUrls);

builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("KTNLocation.Services.ServerRsaKeyStore", LogLevel.Warning);
builder.Logging.AddFilter("KTNLocation.Services.GeoProviders.GeoDataBootstrapper", LogLevel.Warning);

WriteStartupLog("Program", "INFO", "Starting KTNLocation...");
WriteStartupLog("Program", "INFO", $"Environment: {builder.Environment.EnvironmentName}");
WriteStartupLog("Program", "INFO", $"Log Level: {builder.Configuration["Logging:LogLevel:Default"] ?? "Information"}");
WriteStartupLog("Program", "INFO", $"Configured URLs: {string.Join(", ", listenUrls)}");

builder.Services.Configure<KtnSecurityOptions>(builder.Configuration.GetSection(KtnSecurityOptions.SectionName));
builder.Services.Configure<LocationCacheOptions>(builder.Configuration.GetSection(LocationCacheOptions.SectionName));
builder.Services.Configure<GeoProviderOptions>(builder.Configuration.GetSection(GeoProviderOptions.SectionName));
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

var sqliteConn = builder.Configuration.GetConnectionString("SQLite") ?? "Data Source=Data/geonode.db";
builder.Services.AddDbContext<KTNLocationDbContext>(o => o.UseSqlite(sqliteConn));

var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
if (redisOptions.Enabled)
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redisConn;
        o.InstanceName = string.IsNullOrWhiteSpace(redisOptions.InstanceName) ? "KTNLocation:" : redisOptions.InstanceName;
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.Configure<MvcOptions>(o => o.Filters.Add(new ProducesAttribute("application/json")));

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.Services.AddScoped<DbInitializer>();
builder.Services.AddSingleton<GeoDataBootstrapper>();
builder.Services.AddSingleton<ServerRsaKeyStore>();

builder.Services.AddSingleton<IIpGeoProviderService, Ip2RegionProviderService>();
builder.Services.AddSingleton<IIpGeoProviderService, GeoLiteMmdbProviderService>();
builder.Services.AddSingleton<IIpGeoProviderService, LoyalSoldierMmdbProviderService>();
builder.Services.AddSingleton<IIpGeoProviderService, GeoIP2CnMmdbProviderService>();

builder.Services.AddScoped<ICryptoService, RsaCryptoService>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<ILocationService, LocationService>();

builder.Services.AddHostedService<GeoDataUpdateService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (serverOptions.EnableHttps)
{
    app.UseHttpsRedirection();
}

var ktnSecurityOpts = app.Services.GetRequiredService<IOptions<KtnSecurityOptions>>().Value;
if (ktnSecurityOpts.Enabled)
{
    app.UseMiddleware<KtnEncryptionMiddleware>();
}
app.MapControllers();

var admin = app.MapGroup("/admin");
admin.MapGet("/status", () => Results.Json(new
{
    status = "ok",
    version = "1.0.0",
    time = DateTimeOffset.UtcNow
}));
admin.MapPost("/geo/update", async (GeoDataUpdateService svc, CancellationToken ct) =>
{
    await svc.RunUpdateCycleAsync(ct);
    return Results.Json(new { status = "ok", message = "更新检查已完成" });
});

using (var scope = app.Services.CreateScope())
{
    var securityOptions = scope.ServiceProvider.GetRequiredService<IOptions<KtnSecurityOptions>>().Value;
    if (securityOptions.Enabled)
    {
        var safeKeySize = Math.Clamp(securityOptions.RsaKeySize, 1024, 8192);

        WriteStartupLog("KtnSecurity", "DEBUG", "Generating/Loading server PEM key pair...");
        var keyStore = scope.ServiceProvider.GetRequiredService<ServerRsaKeyStore>();
        await keyStore.GetPublicKeyPemAsync();
        WriteStartupLog("KtnSecurity", "INFO", $"RSA KeySize: {safeKeySize}");
        WriteStartupLog("KtnSecurity", "INFO", $"Private PEM: {ResolvePath(securityOptions.ServerPrivateKeyPath)}");
        WriteStartupLog("KtnSecurity", "INFO", $"Public PEM: {ResolvePath(securityOptions.ServerPublicKeyPath)}");
    }

    var geoOptsInst = scope.ServiceProvider.GetRequiredService<IOptions<GeoProviderOptions>>().Value;
    var bootstrapper = scope.ServiceProvider.GetRequiredService<GeoDataBootstrapper>();
    if (geoOptsInst.WaitForDownloadOnStartup)
    {
        WriteStartupLog("GeoData", "DEBUG", "Bootstrapping geo data...");
        await bootstrapper.BootstrapAsync();
        WriteStartupLog("GeoData", "INFO", "Geo data bootstrap completed.");
    }
    else
    {
        WriteStartupLog("GeoData", "DEBUG", "Bootstrapping geo data in background...");
        _ = bootstrapper.BootstrapAsync();
    }

    WriteStartupLog("Database", "DEBUG", "Initializing database...");
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();

    var sqliteDataSource = TryExtractSqliteDataSource(sqliteConn);
    if (!string.IsNullOrWhiteSpace(sqliteDataSource))
    {
        var fullPath = ResolvePath(sqliteDataSource);
        WriteStartupLog("Database", "INFO", $"Initialized: {Path.GetFileName(fullPath)}");
        WriteStartupLog("Database", "DEBUG", $"Full path: {fullPath}");
    }
    else
    {
        WriteStartupLog("Database", "INFO", "Initialized SQLite database.");
    }

    if (redisOptions.Enabled)
    {
        WriteStartupLog("Cache", "INFO", $"Redis enabled. Endpoint: {ExtractRedisEndpoint(redisConn)}");
    }
    else
    {
        WriteStartupLog("Cache", "WARN", "Redis disabled. Using in-memory distributed cache.");
    }
}

var geoOpts = builder.Configuration.GetSection(GeoProviderOptions.SectionName).Get<GeoProviderOptions>() ?? new GeoProviderOptions();
var providerOrder = geoOpts.ProviderOrder is { Length: > 0 }
    ? string.Join(" -> ", geoOpts.ProviderOrder.Distinct(StringComparer.OrdinalIgnoreCase))
    : "(未配置)";

WriteStartupLog("GeoProvider", "INFO", $"ProviderOrder: {providerOrder}");
WriteStartupLog("GeoProvider", "INFO", $"AutoDownloadOnStartup: {geoOpts.AutoDownloadOnStartup}");
WriteStartupLog("GeoProvider", "INFO", $"UpdateIntervalHours: {geoOpts.UpdateIntervalHours}");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var routeEndpoints = app.Services.GetServices<EndpointDataSource>()
        .SelectMany(ds => ds.Endpoints)
        .OfType<RouteEndpoint>()
        .Select(e => e.RoutePattern.RawText)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    WriteStartupLog("ApiRouter", "INFO", $"Registered {routeEndpoints.Length} routes");
    foreach (var route in routeEndpoints)
    {
        WriteStartupLog("ApiRouter", "DEBUG", $"/{route.TrimStart('/')}");
    }

    var startedUrls = app.Urls.Count > 0 ? app.Urls.ToArray() : listenUrls;
    foreach (var url in startedUrls.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            WriteStartupLog("Program", "INFO", $"HTTPS started on {url}");
            continue;
        }

        WriteStartupLog("Program", "INFO", $"Server started on {url}");
    }

    if (app.Environment.IsDevelopment())
    {
        var httpBaseUrl = startedUrls.FirstOrDefault(x => x.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(httpBaseUrl))
        {
            WriteStartupLog("Program", "INFO", $"OpenAPI: {httpBaseUrl.TrimEnd('/')}/openapi/v1.json");
        }
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    WriteStartupLog("Program", "WARN", "Application is shutting down...");
});

app.Lifetime.ApplicationStopped.Register(() =>
{
    WriteStartupLog("Program", "INFO", "Application stopped.");
});

app.Run();

static string[] BuildListenUrls(ServerOptions options)
{
    var address = NormalizeAddress(options.Address);
    var httpPort = NormalizePort(options.HttpPort, 5186);
    var urls = new List<string> { $"http://{address}:{httpPort}" };

    if (options.EnableHttps)
    {
        var httpsPort = NormalizePort(options.HttpsPort, 7044);
        urls.Add($"https://{address}:{httpsPort}");
    }

    return urls.ToArray();
}

static int NormalizePort(int value, int fallback)
{
    return value is > 0 and <= 65535 ? value : fallback;
}

static string NormalizeAddress(string? address)
{
    if (string.IsNullOrWhiteSpace(address))
    {
        return "localhost";
    }

    return address.Trim();
}

static string? TryExtractSqliteDataSource(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return null;
    }

    const string prefix = "Data Source=";
    foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var dataSource = part[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(dataSource) ? null : dataSource;
    }

    return null;
}

static string ExtractRedisEndpoint(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "(empty)";
    }

    var firstSegment = connectionString
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();

    return string.IsNullOrWhiteSpace(firstSegment) ? "(invalid)" : firstSegment;
}

static string ResolvePath(string path)
{
    return Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(path, Directory.GetCurrentDirectory());
}

static void WriteStartupLog(string source, string level, string message)
{
    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    var safeSource = Markup.Escape(source);
    var safeMessage = Markup.Escape(message);
    var normalizedLevel = string.IsNullOrWhiteSpace(level)
        ? "INFO"
        : level.Trim().ToUpperInvariant();

    var levelColor = normalizedLevel switch
    {
        "DEBUG" => "dodgerblue1",
        "INFO" => "green",
        "WARN" => "yellow",
        "ERROR" => "red",
        _ => "silver"
    };

    AnsiConsole.MarkupLine($"[grey][[{timestamp}]][/] [deepskyblue1][[{safeSource}]][/] [{levelColor}][[{normalizedLevel}]][/] {safeMessage}");
}