using System.Globalization;
using KTNLocation.Models.Common;
using KTNLocation.Models.Domain;
using KTNLocation.Models.Dtos.Location;
using KTNLocation.Models.Entities;
using KTNLocation.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KTNLocation.Controllers;

[ApiController]
[Route("api/location")]
public sealed class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationController(ILocationService locationService) => _locationService = locationService;

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> GetCurrentLocation(
        [FromQuery] string? provider, [FromQuery] string? crs, CancellationToken ct)
    {
        var ip = GetRequestIp();
        var result = await _locationService.ResolveAsync(null, null, null, ip, provider, crs, ct);
        return result is null
            ? NotFound(ApiResponse<LocationResponse>.Fail("未匹配到定位信息。"))
            : Ok(ApiResponse<LocationResponse>.Ok(ToResponse(result)));
    }

    [HttpGet("ip")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> LocateByIp(
        [FromQuery] string? ip, [FromQuery] string? provider, CancellationToken ct)
    {
        var candidateIp = string.IsNullOrWhiteSpace(ip) ? GetRequestIp() : ip;
        if (string.IsNullOrWhiteSpace(candidateIp))
            return BadRequest(ApiResponse<LocationResponse>.Fail("IP 为空，且无法从请求中识别客户端 IP。"));

        var result = await _locationService.ResolveByIpAsync(candidateIp, provider, ct);
        return result is null
            ? NotFound(ApiResponse<LocationResponse>.Fail("IP 未命中位置库。"))
            : Ok(ApiResponse<LocationResponse>.Ok(ToResponse(result)));
    }

    [HttpGet("gps")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> LocateByGps(
        [FromQuery] double? latitude, [FromQuery] double? longitude, [FromQuery] string? crs, CancellationToken ct)
    {
        if (!TryResolveGpsInput(latitude, longitude, crs, out var resolvedLatitude, out var resolvedLongitude, out var resolvedCrs, out var errorMessage))
            return BadRequest(ApiResponse<LocationResponse>.Fail(errorMessage));

        var result = await _locationService.ResolveByGpsAsync(resolvedLatitude, resolvedLongitude, resolvedCrs, ct);
        return result is null
            ? NotFound(ApiResponse<LocationResponse>.Fail("GPS 坐标未命中位置库。"))
            : Ok(ApiResponse<LocationResponse>.Ok(ToResponse(result)));
    }

    [HttpPost("resolve")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> Resolve(
        [FromBody] LocationResolveRequest request, CancellationToken ct)
    {
        var ip = GetRequestIp();
        var result = await _locationService.ResolveAsync(request.Ip, request.Latitude, request.Longitude, ip, request.Provider, request.Crs, ct);
        return result is null
            ? NotFound(ApiResponse<LocationResponse>.Fail("无法根据当前输入完成定位。"))
            : Ok(ApiResponse<LocationResponse>.Ok(ToResponse(result)));
    }

    [HttpGet("providers")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetProviders()
        => Ok(ApiResponse<IReadOnlyList<string>>.Ok(_locationService.GetProviderNames()));

    [HttpGet("providers/{provider}/ip")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> LocateBySpecificProvider(
        [FromRoute] string provider, [FromQuery] string? ip, CancellationToken ct)
    {
        var candidateIp = string.IsNullOrWhiteSpace(ip) ? GetRequestIp() : ip;
        if (string.IsNullOrWhiteSpace(candidateIp))
            return BadRequest(ApiResponse<LocationResponse>.Fail("IP 为空，且无法从请求中识别客户端 IP。"));

        var result = await _locationService.ResolveByProviderAsync(provider, candidateIp, ct);
        return result is null
            ? NotFound(ApiResponse<LocationResponse>.Fail($"服务 {provider} 未命中定位结果。"))
            : Ok(ApiResponse<LocationResponse>.Ok(ToResponse(result)));
    }

    [HttpGet("library/counties")]
    public async Task<ActionResult<ApiResponse<PagedResult<CountyLibraryItemResponse>>>> GetCountyLibrary(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var paged = await _locationService.QueryCountyLibraryAsync(keyword, page, pageSize, ct);
        var mapped = new PagedResult<CountyLibraryItemResponse>
        {
            Items = paged.Items.Select(x => new CountyLibraryItemResponse
            {
                Id = x.Id, Country = x.Country, Province = x.Province,
                City = x.City, County = x.County, Latitude = x.Latitude, Longitude = x.Longitude
            }).ToList(),
            Total = paged.Total, Page = paged.Page, PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PagedResult<CountyLibraryItemResponse>>.Ok(mapped));
    }

    [HttpGet("library/ip-ranges")]
    public async Task<ActionResult<ApiResponse<PagedResult<IpRangeLibraryItemResponse>>>> GetIpLibrary(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var paged = await _locationService.QueryIpLibraryAsync(keyword, page, pageSize, ct);
        var mapped = new PagedResult<IpRangeLibraryItemResponse>
        {
            Items = paged.Items.Select(x => new IpRangeLibraryItemResponse
            {
                Id = x.Id, StartIp = x.StartIp, EndIp = x.EndIp, Country = x.Country,
                Province = x.Province, City = x.City, County = x.County, Latitude = x.Latitude, Longitude = x.Longitude
            }).ToList(),
            Total = paged.Total, Page = paged.Page, PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PagedResult<IpRangeLibraryItemResponse>>.Ok(mapped));
    }

    [HttpPost("library/county")]
    public async Task<ActionResult<ApiResponse<CountyLibraryItemResponse>>> AddCounty(
        [FromBody] CountyLocationCreateRequest request, CancellationToken ct)
    {
        var created = await _locationService.AddCountyAsync(new CountyLocation
        {
            Country = "中国", Province = request.Province, City = request.City,
            County = request.County, Latitude = request.Latitude, Longitude = request.Longitude
        }, ct);
        return Ok(ApiResponse<CountyLibraryItemResponse>.Ok(new CountyLibraryItemResponse
        {
            Id = created.Id, Country = created.Country, Province = created.Province,
            City = created.City, County = created.County, Latitude = created.Latitude, Longitude = created.Longitude
        }, "县级位置已添加。"));
    }

    [HttpPost("library/ip-range")]
    public async Task<ActionResult<ApiResponse<IpRangeLibraryItemResponse>>> AddIpRange(
        [FromBody] IpRangeCreateRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _locationService.AddIpRangeAsync(new IpRangeLocation
            {
                StartIp = request.StartIp, EndIp = request.EndIp, Country = "中国",
                Province = request.Province, City = request.City, County = request.County,
                Latitude = request.Latitude, Longitude = request.Longitude
            }, ct);
            return Ok(ApiResponse<IpRangeLibraryItemResponse>.Ok(new IpRangeLibraryItemResponse
            {
                Id = created.Id, StartIp = created.StartIp, EndIp = created.EndIp,
                Country = created.Country, Province = created.Province, City = created.City,
                County = created.County, Latitude = created.Latitude, Longitude = created.Longitude
            }, "IP 位置段已添加。"));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<IpRangeLibraryItemResponse>.Fail(ex.Message)); }
    }

    private string? GetRequestIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private bool TryResolveGpsInput(
        double? latitude,
        double? longitude,
        string? crs,
        out double resolvedLatitude,
        out double resolvedLongitude,
        out string? resolvedCrs,
        out string errorMessage)
    {
        resolvedLatitude = default;
        resolvedLongitude = default;
        resolvedCrs = NormalizeCrs(crs);
        errorMessage = string.Empty;

        if (latitude.HasValue ^ longitude.HasValue)
        {
            errorMessage = "latitude 与 longitude 需要同时提供。";
            return false;
        }

        if (latitude.HasValue && longitude.HasValue)
        {
            resolvedLatitude = latitude.Value;
            resolvedLongitude = longitude.Value;
            if (string.IsNullOrWhiteSpace(resolvedCrs))
                resolvedCrs = NormalizeCrs(Request.Headers["X-Geo-Crs"].ToString());
            return true;
        }

        var latitudeFromHeader = Request.Headers["X-Geo-Latitude"].ToString();
        var longitudeFromHeader = Request.Headers["X-Geo-Longitude"].ToString();
        if (string.IsNullOrWhiteSpace(latitudeFromHeader) || string.IsNullOrWhiteSpace(longitudeFromHeader))
        {
            errorMessage = "未检测到 GPS 坐标。请先在前端申请定位权限后再调用该接口。可通过 query(latitude/longitude) 或请求头 X-Geo-Latitude、X-Geo-Longitude 传入坐标。";
            return false;
        }

        if (!TryParseCoordinate(latitudeFromHeader, out resolvedLatitude)
            || !TryParseCoordinate(longitudeFromHeader, out resolvedLongitude))
        {
            errorMessage = "GPS 坐标格式无效，latitude/longitude 需为数字。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedCrs))
            resolvedCrs = NormalizeCrs(Request.Headers["X-Geo-Crs"].ToString());

        return true;
    }

    private static string? NormalizeCrs(string? crs)
        => string.IsNullOrWhiteSpace(crs) ? null : crs.Trim();

    private static bool TryParseCoordinate(string raw, out double value)
        => double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
           || double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);

    private static LocationResponse ToResponse(LocationResolvedResult r) => new()
    {
        Source = r.Source, Ip = r.Ip, Country = r.Country, Province = r.Province,
        City = r.City, County = r.County, Latitude = r.Latitude, Longitude = r.Longitude,
        DistanceKm = r.DistanceKm, ResolvedAt = r.ResolvedAt
    };
}
