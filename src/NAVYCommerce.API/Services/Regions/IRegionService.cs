using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Services.Regions;

public interface IRegionService
{
    Task<IReadOnlyList<RegionResponse>> GetProvincesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegionResponse>> GetCitiesAsync(
        string provinceCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegionResponse>> GetDistrictsAsync(
        string cityCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegionResponse>> GetVillagesAsync(
        string districtCode,
        CancellationToken cancellationToken = default);

    Task<ResolvedRegion?> ResolveVillageAsync(
        string villageCode,
        CancellationToken cancellationToken = default);
}
