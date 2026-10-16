using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Services.Regions;

// Hasil lookup lengkap untuk penyimpanan alamat; tiap tingkat memakai tipe yang sama.
public sealed record ResolvedRegion(
    RegionResponse Province,
    RegionResponse City,
    RegionResponse District,
    RegionResponse Village);
