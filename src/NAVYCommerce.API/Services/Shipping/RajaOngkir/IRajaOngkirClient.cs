namespace NAVYCommerce.API.Services.Shipping.RajaOngkir;

public interface IRajaOngkirClient
{
    Task<IReadOnlyList<ShippingDestination>> SearchDestinationsAsync(
        string search, int limit = 20, int offset = 0,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        int originId, int destinationId, int weightGrams,
        CancellationToken cancellationToken = default);
}
