namespace NAVYCommerce.API.Services.Shipping.RajaOngkir;

public sealed record ShippingDestination(
    int Id, string Label, string Province, string City,
    string District, string Village, string PostalCode);

public sealed record ShippingRate(
    string CourierCode, string CourierName, string Service,
    string Description, decimal Cost, string EstimatedDelivery);

public sealed class ShippingProviderException : Exception
{
    public int ResponseStatusCode { get; }

    public ShippingProviderException(string message, int responseStatusCode)
        : base(message)
    {
        ResponseStatusCode = responseStatusCode;
    }
}
