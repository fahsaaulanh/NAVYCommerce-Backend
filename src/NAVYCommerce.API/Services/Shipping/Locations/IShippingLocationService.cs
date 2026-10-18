using NAVYCommerce.API.Services.Shipping.RajaOngkir;

namespace NAVYCommerce.API.Services.Shipping.Locations;

public sealed record ShippingLocationCandidate(
    ShippingDestination Destination, bool MatchesAddress);

public interface IShippingLocationService
{
    Task<int?> FindConfirmedIdAsync(ShippingRegion region,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShippingLocationCandidate>> FindCandidatesAsync(
        ShippingRegion region, int offset = 0,
        CancellationToken cancellationToken = default);

    Task<int> ConfirmAsync(ShippingRegion region, int destinationId,
        int searchOffset = 0, CancellationToken cancellationToken = default);
}
