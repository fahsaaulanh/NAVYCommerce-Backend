using NAVYCommerce.API.Services.Shipping.RajaOngkir;

namespace NAVYCommerce.API.Services.Shipping.Locations;

public sealed partial class ShippingLocationService
{
    private const int SearchPageSize = 50;
    private const int MaxSearchPages = 3;
    private const int MaxSearchCallsPerScope = 8;
    private int _automaticSearchCalls;
    private static string AutomaticCacheKey(string fingerprint) => Provider + ":auto-v1:" + fingerprint;

    public async Task<ShippingLocationResolution> ResolveAsync(ShippingRegion region,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint(region);
        var existingId = await FindConfirmedIdAsync(region, cancellationToken);
        if (existingId.HasValue)
            return new(ShippingLocationResolutionStatus.Resolved, existingId.Value);

        var gate = _coordinator.GetGate(fingerprint);
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Request lain mungkin selesai menyimpan saat kita menunggu.
            existingId = await FindConfirmedIdAsync(region, cancellationToken);
            if (existingId.HasValue)
                return new(ShippingLocationResolutionStatus.Resolved, existingId.Value);

            var key = AutomaticCacheKey(fingerprint);
            var cached = _coordinator.GetFailure(key);
            if (cached != null) return cached;

            ShippingLocationResolution Failure(ShippingLocationResolutionStatus status)
            {
                var result = new ShippingLocationResolution(status);
                _coordinator.RememberFailure(key, result);
                return result;
            }

            var seenIds = new HashSet<int>();
            ShippingDestination? selected = null;
            for (var page = 0; page < MaxSearchPages; page++)
            {
                if (++_automaticSearchCalls > MaxSearchCallsPerScope)
                    return new(ShippingLocationResolutionStatus.RequestBudgetExceeded);

                IReadOnlyList<ShippingDestination> destinations;
                try
                {
                    destinations = await _client.SearchDestinationsAsync(region.PostalCode.Trim(),
                        SearchPageSize, page * SearchPageSize, cancellationToken);
                }
                catch (ShippingProviderException exception)
                {
                    _logger.LogWarning(exception, "Pencarian lokasi otomatis provider {Provider} gagal.", Provider);
                    return Failure(ShippingLocationResolutionStatus.ProviderUnavailable);
                }

                if (destinations is null || destinations.Count > SearchPageSize)
                    return Failure(ShippingLocationResolutionStatus.ProviderUnavailable);

                foreach (var destination in destinations)
                {
                    // Pagination berulang/bergeser tidak dianggap bukti hasil lengkap.
                    if (destination is null || destination.Id <= 0 || !seenIds.Add(destination.Id))
                        return Failure(ShippingLocationResolutionStatus.SearchIncomplete);
                    if (!Matches(region, destination)) continue;
                    if (selected != null)
                        return Failure(ShippingLocationResolutionStatus.Ambiguous);
                    selected = destination;
                }

                if (destinations.Count == SearchPageSize) continue;

                // Hanya simpan jika halaman terakhir sudah ditemukan dan tepat satu ID cocok.
                if (selected == null) return Failure(ShippingLocationResolutionStatus.NotFound);
                try
                {
                    var id = await PersistMappingAsync(fingerprint, selected.Id,
                        selected.Label, cancellationToken);
                    _coordinator.Forget(key);
                    return new(ShippingLocationResolutionStatus.Resolved, id);
                }
                catch (MappingConflictException)
                {
                    return Failure(ShippingLocationResolutionStatus.MappingConflict);
                }
            }
            return Failure(ShippingLocationResolutionStatus.SearchIncomplete);
        }
        finally { gate.Release(); }
    }
}
