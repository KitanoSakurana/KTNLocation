# KTNLocation

> .NET 9 location API with optional encryption, SQLite persistence, and pluggable GeoIP providers.

> **Note:** This is a casual C# practice project. I can't guarantee "enterprise-grade" code quality here. My philosophy is **not to obsess over writing perfect or flawless code, but to create something that feels intuitive and looks right to me.** (Though I’d be happy if it ends up being useful to you, too!)

[中文文档](README_CN.md)

## Why this project

KTNLocation provides a single HTTP API for:

- IP location (provider-based + SQLite fallback)
- GPS nearest-county resolving
- Optional encrypted API response (protocol)
- Optional Redis cache (with in-memory fallback)

## Tech stack

- .NET 9 / ASP.NET Core Web API
- SQLite (primary data storage)
- Redis (optional distributed cache)
- Spectre.Console (structured startup logs)

## Quick start

### Requirements

- .NET 9 SDK
- Redis (optional)

### Run

```bash
dotnet run --project .\Api
```

Swagger (Requires `DebugMode: true`):

- `http://localhost:[PORT]/swagger`

## Configuration

Primary config file: `Api/appsettings.json`  
Development overrides only: `Api/appsettings.Development.json` (recommended for logging overrides)

### Key sections

- `ConnectionStrings`
  - `SQLite`
  - `Redis`
- `Server`
  - `Address`, `HttpPort`, `EnableHttps`, `HttpsPort`
  - `DebugMode`: Controls `[DEBUG]` logs and Swagger UI visibility.
  - `HttpsCertificatePath`, `HttpsPrivateKeyPath`, `HttpsCertificatePassword`: PEM certificate configurations for HTTPS.
- `Redis`
  - `Enabled`: `true` uses Redis, `false` falls back to in-memory cache
  - `InstanceName`
- `KtnSecurity`
  - `RsaKeySize`
  - `ServerPrivateKeyPath`
  - `ServerPublicKeyPath`
- `Cache`
  - `DefaultTtlSeconds`, `IpTtlSeconds`, `GpsTtlSeconds`
- `GeoProviders`
  - `ProviderOrder`, auto-download/update settings, dataset paths

## API overview

### Crypto

- `GET /api/crypto/server-public-key`
- `POST /api/crypto/client-public-key`
- `POST /api/crypto/key-pair/generate`
- `POST /api/crypto/decrypt-with-server`

### Location

- `GET /api/location/current`
- `GET /api/location/ip`
- `GET /api/location/gps`
- `POST /api/location/resolve`
- `GET /api/location/providers`
- `GET /api/location/providers/{provider}/ip`
- `GET /api/location/library/counties`
- `GET /api/location/library/ip-ranges`
- `POST /api/location/library/county`
- `POST /api/location/library/ip-range`

> For browser usage, prefer the homepage button **"Request location and call /api/location/gps"**.
> It requests geolocation permission first, then calls the API automatically.
> Programmatic clients can still send coordinates using query params (`latitude`, `longitude`, `crs`) or headers (`X-Geo-Latitude`, `X-Geo-Longitude`, `X-Geo-Crs`).

### Admin

- `GET /admin/status`
- `POST /admin/geo/update`

## Security notes

- Server PEM files are auto-generated/loaded on startup.
- Default output paths:
  - `Api/KtnSecurity/server_private.pem`
  - `Api/KtnSecurity/server_public.pem`
- Never expose or commit private keys in public repositories.

## Third-party datasets and compliance

This repository contains integration logic. Dataset licenses remain with upstream projects.

- ip2region: Apache-2.0 (project code license)
- Loyalsoldier/geoip: CC-BY-SA-4.0 / GPL-3.0
- GeoLite data: subject to MaxMind EULA and attribution requirements

## License

Source code is licensed under the [MIT License](LICENSE).
