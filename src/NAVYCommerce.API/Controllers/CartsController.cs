using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAVYCommerce.API.Contracts.Carts;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services.Carts;

namespace NAVYCommerce.API.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize(Roles = "Customer")]
  [Produces("application/json")]
  [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public sealed class CartsController : ControllerBase
  {
    private readonly ICartService _cartService;

    public CartsController(ICartService cartService)
    {
      _cartService = cartService;
    }

    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
      if (!User.TryGetUserId(out var customerId))
      {
        return InvalidIdentity();
      }

      var cart = await _cartService.GetAsync(
          customerId,
          cancellationToken);

      return cart == null
          ? InvalidIdentity()
          : Ok(new { data = cart });
    }

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
      if (!User.TryGetUserId(out var customerId)) return InvalidIdentity();

      var summary = await _cartService.GetSummaryAsync(customerId, cancellationToken);
      return summary == null ? InvalidIdentity() : Ok(new { data = summary });
    }

    [HttpPost("items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
      if (!User.TryGetUserId(out var customerId))
      {
        return InvalidIdentity();
      }

      var result = await _cartService.AddItemAsync(
          customerId,
          request.ProductVariantId!.Value,
          request.Quantity!.Value,
          cancellationToken);

      return ToMutationResponse(
          result,
          "CART_ITEM_ADDED",
          "Produk berhasil ditambahkan ke keranjang.");
    }

    [HttpPut("items/{cartItemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItemQuantity(
        Guid cartItemId,
        [FromBody] UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
      if (!User.TryGetUserId(out var customerId))
      {
        return InvalidIdentity();
      }

      var result = await _cartService.UpdateItemQuantityAsync(
          customerId,
          cartItemId,
          request.Quantity!.Value,
          cancellationToken);

      return ToMutationResponse(
          result,
          "CART_ITEM_UPDATED",
          "Jumlah produk berhasil diperbarui.");
    }

    [HttpDelete("items/{cartItemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(
        Guid cartItemId,
        CancellationToken cancellationToken)
    {
      if (!User.TryGetUserId(out var customerId))
      {
        return InvalidIdentity();
      }

      var result = await _cartService.RemoveItemAsync(
          customerId,
          cartItemId,
          cancellationToken);

      return ToMutationResponse(
          result,
          "CART_ITEM_REMOVED",
          "Produk berhasil dihapus dari keranjang.");
    }

    private IActionResult ToMutationResponse(
        CartMutationResult result,
        string successCode,
        string successMessage)
    {
      return result.Status switch
      {
        CartMutationStatus.Succeeded => Ok(new
        {
          code = successCode,
          message = successMessage,
          data = new { itemId = result.ItemId }
        }),

        CartMutationStatus.CustomerNotFound => InvalidIdentity(),

        CartMutationStatus.VariantNotFound => NotFound(new
        {
          code = "CART_VARIANT_NOT_FOUND",
          message = "Varian produk tidak ditemukan."
        }),

        CartMutationStatus.ItemNotFound => NotFound(new
        {
          code = "CART_ITEM_NOT_FOUND",
          message = "Item tidak ditemukan di keranjangmu."
        }),

        CartMutationStatus.ProductUnavailable => Conflict(new
        {
          code = "CART_PRODUCT_UNAVAILABLE",
          message = "Produk sedang tidak tersedia untuk dibeli."
        }),

        CartMutationStatus.InsufficientStock => Conflict(new
        {
          code = "CART_STOCK_INSUFFICIENT",
          message = "Jumlah yang diminta melebihi stok tersedia.",
          data = new { availableStock = result.AvailableStock }
        }),

        CartMutationStatus.InvalidQuantity => BadRequest(new
        {
          code = "CART_QUANTITY_INVALID",
          message = "Jumlah harus lebih dari 0 dan tidak melebihi batas integer.",
          errors = new
          {
            quantity = new[] { "Jumlah produk tidak valid." }
          }
        }),

        _ => throw new InvalidOperationException(
            "Status perubahan keranjang tidak dikenali.")
      };
    }

    private IActionResult InvalidIdentity()
    {
      return Unauthorized(new
      {
        code = "AUTH_INVALID_IDENTITY",
        message = "Identitas Customer tidak valid. Silakan login kembali."
      });
    }
  }
}
