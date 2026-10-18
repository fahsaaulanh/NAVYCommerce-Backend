using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NAVYCommerce.API.Contracts.Checkout;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services.Checkout.Shipping;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/checkout/shipping")]
[EnableRateLimiting("checkout-shipping")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutShippingController : ControllerBase
{
    private readonly ICheckoutShippingService _shipping;
    public CheckoutShippingController(ICheckoutShippingService shipping) => _shipping = shipping;

    [HttpPost("quotes")]
    public async Task<IActionResult> Calculate([FromBody] CheckoutShippingRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var customerId))
            return Unauthorized(new { code = "AUTH_INVALID_IDENTITY", message = "Identitas akun tidak valid." });

        var result = await _shipping.CalculateAsync(customerId, request, cancellationToken);
        return result.Status switch
        {
            CheckoutShippingStatus.Calculated => Ok(new { data = result.Data }),
            CheckoutShippingStatus.CustomerUnavailable => StatusCode(403, new
            {
                code = "CHECKOUT_CUSTOMER_UNAVAILABLE",
                message = "Checkout memerlukan akun Customer yang sudah terverifikasi."
            }),
            CheckoutShippingStatus.AddressNotFound => NotFound(new
            {
                code = "CHECKOUT_ADDRESS_NOT_FOUND", message = "Alamat pengiriman tidak ditemukan."
            }),
            CheckoutShippingStatus.InvalidCartItems => BadRequest(new
            {
                code = "CHECKOUT_CART_INVALID",
                message = "Sebagian item tidak tersedia, stok tidak cukup, atau item bukan milik akunmu. Muat ulang keranjang.",
                invalidCartItemIds = result.InvalidCartItemIds ?? Array.Empty<Guid>()
            }),
            CheckoutShippingStatus.TooManyStores => BadRequest(new
            {
                code = "CHECKOUT_STORE_LIMIT", message = "Maksimal 5 toko dalam satu preview checkout."
            }),
            CheckoutShippingStatus.DestinationNotMapped => UnprocessableEntity(new
            {
                code = "CHECKOUT_DESTINATION_UNMAPPED",
                message = "Wilayah alamat tujuan belum didukung. Pilih alamat lain atau hubungi pengelola demo."
            }),
            _ => throw new InvalidOperationException("Status preview ongkir tidak dikenali.")
        };
    }
}
