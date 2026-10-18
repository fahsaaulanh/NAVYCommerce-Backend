using NAVYCommerce.API.Services.Shipping.RajaOngkir;

namespace NAVYCommerce.API.Contracts.Checkout;

// Gunakan kembali ShippingRate yang sudah dihasilkan client RajaOngkir.
public sealed record CheckoutShippingPackageResponse(
    Guid StoreId,
    string StoreName,
    IReadOnlyList<Guid> CartItemIds,
    int WeightGrams,
    decimal ItemsSubtotal,
    string Status,
    string? Message,
    IReadOnlyList<ShippingRate> Rates);

public sealed record CheckoutShippingResponse(
    Guid AddressId,
    DateTime CalculatedAt,
    bool IsComplete,
    IReadOnlyList<CheckoutShippingPackageResponse> Packages);
