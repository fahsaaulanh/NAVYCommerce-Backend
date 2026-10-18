namespace NAVYCommerce.API.Services.Shipping.Locations;

public enum ShippingLocationResolutionStatus
{
    Resolved,
    NotFound,
    Ambiguous,
    SearchIncomplete,
    ProviderUnavailable,
    RequestBudgetExceeded,
    MappingConflict
}

public sealed record ShippingLocationResolution(
    ShippingLocationResolutionStatus Status, int? DestinationId = null)
{
    public bool IsResolved => Status == ShippingLocationResolutionStatus.Resolved && DestinationId > 0;
    public bool IsTemporary => Status is ShippingLocationResolutionStatus.ProviderUnavailable
        or ShippingLocationResolutionStatus.RequestBudgetExceeded;

    public string Code => Status switch
    {
        ShippingLocationResolutionStatus.NotFound => "UNMAPPED",
        ShippingLocationResolutionStatus.Ambiguous => "AMBIGUOUS",
        ShippingLocationResolutionStatus.SearchIncomplete => "SEARCH_INCOMPLETE",
        ShippingLocationResolutionStatus.ProviderUnavailable => "PROVIDER_UNAVAILABLE",
        ShippingLocationResolutionStatus.RequestBudgetExceeded => "RETRY_REQUIRED",
        ShippingLocationResolutionStatus.MappingConflict => "MAPPING_CONFLICT",
        _ => "RESOLVED"
    };

    public string Message => Status switch
    {
        ShippingLocationResolutionStatus.NotFound =>
            "Belum ditemukan lokasi pengiriman yang cocok. Periksa wilayah dan kode pos alamat atau gunakan alamat lain.",
        ShippingLocationResolutionStatus.Ambiguous =>
            "Lokasi pengiriman belum dapat dipastikan karena ada beberapa hasil yang cocok. Silakan gunakan alamat lain atau hubungi pengelola.",
        ShippingLocationResolutionStatus.SearchIncomplete =>
            "Pemeriksaan wilayah belum lengkap. Lokasi tidak dipilih otomatis; silakan gunakan alamat lain atau hubungi pengelola.",
        ShippingLocationResolutionStatus.ProviderUnavailable =>
            "Layanan pencarian lokasi sedang tidak tersedia. Silakan coba kembali beberapa saat lagi.",
        ShippingLocationResolutionStatus.RequestBudgetExceeded =>
            "Sebagian lokasi sudah diperiksa. Silakan hitung ongkir kembali untuk melanjutkan pemeriksaan lokasi lainnya.",
        ShippingLocationResolutionStatus.MappingConflict =>
            "Lokasi pengiriman memerlukan pemeriksaan pengelola. Silakan gunakan alamat lain.",
        _ => "Lokasi pengiriman tersedia."
    };
}
