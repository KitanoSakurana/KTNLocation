# KTNLocation

> 基于 .NET 9 的定位 API，支持可选 加密、SQLite 持久化与可插拔 GeoIP 服务。

> 留言：随手写的 C# 练习项目，不保证代码质量一定很好，我主打 **不追求写出多好，多完美的代码，只希望做出来的东西自己看着舒服、用着顺手。**（虽然我也希望它能对别人有用）

[English](README.md)

## 项目定位

KTNLocation 提供统一 HTTP 接口，覆盖：

- IP 定位（多 Provider + SQLite 回落）
- GPS 县级就近匹配
- 可选 加密响应
- 可选 Redis 缓存（可自动回退内存缓存）

## 技术栈

- .NET 9 / ASP.NET Core Web API
- SQLite（主存储）
- Redis（可选分布式缓存）
- Spectre.Console（结构化启动日志）

## 快速开始

### 环境要求

- .NET 9 SDK
- Redis（可选）

### 启动

```bash
dotnet run --project .\Api
```

Swagger 地址（需开启 `DebugMode: true`）：

- `http://localhost:[PORT]/swagger`

## 配置说明

主配置文件：`Api/appsettings.json`  
开发覆盖文件：`Api/appsettings.Development.json`（建议仅覆盖日志级别）

### 关键配置段

- `ConnectionStrings`
  - `SQLite`
  - `Redis`
- `Server`
  - `Address`、`HttpPort`、`EnableHttps`、`HttpsPort`
  - `DebugMode`：控制 `[DEBUG]` 级别日志输出及 Swagger UI 访问。
  - `HttpsCertificatePath`、`HttpsPrivateKeyPath`、`HttpsCertificatePassword`：HTTPS 的 PEM 格式证书配置。
- `Redis`
  - `Enabled`：`true` 使用 Redis，`false` 回退为内存缓存
  - `InstanceName`
- `KtnSecurity`
  - `RsaKeySize`
  - `ServerPrivateKeyPath`
  - `ServerPublicKeyPath`
- `Cache`
  - `DefaultTtlSeconds`、`IpTtlSeconds`、`GpsTtlSeconds`
- `GeoProviders`
  - `ProviderOrder`、自动下载/更新配置、数据文件路径

## 接口概览

### 加密相关

- `GET /api/crypto/server-public-key`
- `POST /api/crypto/client-public-key`
- `POST /api/crypto/key-pair/generate`
- `POST /api/crypto/decrypt-with-server`

### 定位相关

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

### 管理接口

- `GET /admin/status`
- `POST /admin/geo/update`

## 安全说明

- 服务启动时自动生成或加载服务端 PEM。
- 默认输出路径：
  - `Api/KtnSecurity/server_private.pem`
  - `Api/KtnSecurity/server_public.pem`
- 请勿将私钥提交到公开仓库。

## 第三方数据许可提示

本仓库主要提供接入与业务逻辑，第三方数据文件许可遵循其上游协议：

- ip2region：Apache-2.0（项目代码）
- Loyalsoldier/geoip：CC-BY-SA-4.0 / GPL-3.0
- GeoLite 数据：需遵守 MaxMind EULA 与署名要求

## 开源许可

本项目源代码采用 [MIT License](LICENSE)。
