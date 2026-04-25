using KTNLocation.Models.Common;
using KTNLocation.Models.Domain;
using KTNLocation.Models.Entities;

namespace KTNLocation.Services.Interfaces;

public interface ILocationService
{
    IReadOnlyList<string> GetProviderNames();
    Task<LocationResolvedResult?> ResolveByIpAsync(string ip, CancellationToken cancellationToken = default);
    Task<LocationResolvedResult?> ResolveByIpAsync(string ip, string? provider, CancellationToken cancellationToken = default);
    Task<LocationResolvedResult?> ResolveByProviderAsync(string provider, string ip, CancellationToken cancellationToken = default);
    Task<LocationResolvedResult?> ResolveByGpsAsync(double latitude, double longitude, string? crs = null, CancellationToken cancellationToken = default);
    Task<LocationResolvedResult?> ResolveAsync(string? ip, double? latitude, double? longitude, string? fallbackIp, string? provider, string? crs = null, CancellationToken cancellationToken = default);
    Task<PagedResult<CountyLocation>> QueryCountyLibraryAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<IpRangeLocation>> QueryIpLibraryAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CountyLocation> AddCountyAsync(CountyLocation input, CancellationToken cancellationToken = default);
    Task<IpRangeLocation> AddIpRangeAsync(IpRangeLocation input, CancellationToken cancellationToken = default);
}
