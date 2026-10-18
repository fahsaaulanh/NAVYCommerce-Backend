using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAVYCommerce.API.Contracts.Stores;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services.Stores.ShippingOrigins;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Route("api/Stores/{storeId:guid}/shipping-origin")]
[Authorize(Roles = "Seller")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class StoreShippingOriginsController : ControllerBase
{
  private readonly IStoreShippingOriginService _origins;
  private readonly ILogger<StoreShippingOriginsController> _logger;

  public StoreShippingOriginsController(
      IStoreShippingOriginService origins,
      ILogger<StoreShippingOriginsController> logger)
  {
    _origins = origins;
    _logger = logger;
  }

  [HttpGet]
  public Task<IActionResult> Get(Guid storeId, CancellationToken cancellationToken)
      => HandleAsync(storeId,
          ownerId => _origins.GetAsync(ownerId, storeId, cancellationToken),
          false, cancellationToken);

  [HttpPut]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
  public Task<IActionResult> Save(
      Guid storeId, [FromBody] SaveStoreShippingOriginRequest request,
      CancellationToken cancellationToken)
      => HandleAsync(storeId,
          ownerId => _origins.SaveAsync(ownerId, storeId, request, cancellationToken),
          true, cancellationToken);

  private async Task<IActionResult> HandleAsync(
      Guid storeId,
      Func<Guid, Task<StoreShippingOriginResult>> operation,
      bool saving,
      CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var ownerId)) return Unauthorized(new
    {
      code = "AUTH_INVALID_IDENTITY",
      message = "Identitas user tidak valid. Silakan login kembali."
    });

    try
    {
      var result = await operation(ownerId);
      return result.Status switch
      {
        StoreShippingOriginStatus.Succeeded => saving
            ? Ok(new
            {
              code = "STORE_SHIPPING_ORIGIN_SAVED",
              message = "Alamat asal pengiriman berhasil disimpan.",
              data = result.Origin
            })
            : Ok(new { data = result.Origin }),
        StoreShippingOriginStatus.StoreNotFound => NotFound(new
        {
          code = "STORE_NOT_FOUND",
          message = "Toko tidak ditemukan atau tidak dapat diakses."
        }),
        StoreShippingOriginStatus.InvalidRegion => BadRequest(new
        {
          code = "STORE_SHIPPING_ORIGIN_REGION_INVALID",
          message = "Kelurahan atau desa yang dipilih tidak tersedia.",
          errors = new { villageCode = new[] { "Pilih kembali kelurahan atau desa." } }
        }),
        StoreShippingOriginStatus.RegionServiceUnavailable => StatusCode(
            StatusCodes.Status503ServiceUnavailable, new
            {
              code = "STORE_SHIPPING_ORIGIN_REGION_UNAVAILABLE",
              message = "Wilayah asal pengiriman belum dapat diperiksa. Silakan coba kembali."
            }),
        _ => throw new InvalidOperationException("Status origin toko tidak dikenali.")
      };
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      _logger.LogError(exception,
          "Operasi origin pengiriman toko {StoreId} oleh owner {OwnerId} gagal.",
          storeId, ownerId);
      return StatusCode(StatusCodes.Status500InternalServerError, new
      {
        code = "STORE_SHIPPING_ORIGIN_OPERATION_FAILED",
        message = "Permintaan alamat asal belum dapat dipastikan. Muat ulang data sebelum mencoba kembali."
      });
    }
  }
}
