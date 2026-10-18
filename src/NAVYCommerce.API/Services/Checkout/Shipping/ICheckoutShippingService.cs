using NAVYCommerce.API.Contracts.Checkout;

namespace NAVYCommerce.API.Services.Checkout.Shipping;

public enum CheckoutShippingStatus
{
    Calculated,
    CustomerUnavailable,
    AddressNotFound,
    InvalidCartItems,
    TooManyStores,
    DestinationNotMapped
}

public sealed record CheckoutShippingResult(
    CheckoutShippingStatus Status,
    CheckoutShippingResponse? Data = null,
    IReadOnlyList<Guid>? InvalidCartItemIds = null);

public interface ICheckoutShippingService
{
    Task<CheckoutShippingResult> CalculateAsync(
        Guid customerId, CheckoutShippingRequest request,
        CancellationToken cancellationToken = default);
}
