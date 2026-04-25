using KTNLocation.Models.Domain;

namespace KTNLocation.Services.Interfaces;

public interface IIpGeoProviderService
{
    string Name { get; }
    Task<LocationResolvedResult?> ResolveAsync(string ip, CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
