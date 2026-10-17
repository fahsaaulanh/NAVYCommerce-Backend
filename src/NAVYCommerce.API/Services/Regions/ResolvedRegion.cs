using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Services.Regions;

public sealed record ResolvedRegion(
    RegionResponse Province,
    RegionResponse City,
    RegionResponse District,
    RegionResponse Village);
