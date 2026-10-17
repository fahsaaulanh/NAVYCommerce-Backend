using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NAVYCommerce.API.Contracts.Addresses;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services.Addresses;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class CustomerAddressesController : ControllerBase
{
  private readonly ICustomerAddressService _addresses;
  private readonly ILogger<CustomerAddressesController> _logger;

  public CustomerAddressesController(
      ICustomerAddressService addresses,
      ILogger<CustomerAddressesController> logger)
  {
    _addresses = addresses;
    _logger = logger;
  }

  [HttpGet("mine")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public Task<IActionResult> GetMine(CancellationToken cancellationToken)
      => HandleAsync(async customerId =>
      {
        var addresses = await _addresses.GetMineAsync(customerId, cancellationToken);
        return addresses == null ? InvalidIdentity() : Ok(new { data = addresses });
      }, cancellationToken);

  [HttpPost]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
  public Task<IActionResult> Create(
      [FromBody] SaveCustomerAddressRequest request,
      CancellationToken cancellationToken)
      => HandleAsync(async customerId => ToResponse(
          await _addresses.CreateAsync(customerId, request, cancellationToken),
          "ADDRESS_CREATED", "Alamat berhasil ditambahkan.",
          StatusCodes.Status201Created), cancellationToken);

  [HttpPut("{addressId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
  public Task<IActionResult> Update(
      Guid addressId,
      [FromBody] SaveCustomerAddressRequest request,
      CancellationToken cancellationToken)
      => HandleAsync(async customerId => ToResponse(
          await _addresses.UpdateAsync(customerId, addressId, request, cancellationToken),
          "ADDRESS_UPDATED", "Alamat berhasil diperbarui."), cancellationToken);

  [HttpPut("{addressId:guid}/default")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public Task<IActionResult> SetDefault(
      Guid addressId,
      CancellationToken cancellationToken)
      => HandleAsync(async customerId => ToResponse(
          await _addresses.SetDefaultAsync(customerId, addressId, cancellationToken),
          "ADDRESS_DEFAULT_UPDATED", "Alamat utama berhasil ditetapkan."), cancellationToken);

  [HttpDelete("{addressId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public Task<IActionResult> Delete(
      Guid addressId,
      CancellationToken cancellationToken)
      => HandleAsync(async customerId => ToResponse(
          await _addresses.DeleteAsync(customerId, addressId, cancellationToken),
          "ADDRESS_DELETED", "Alamat berhasil dihapus."), cancellationToken);

  private async Task<IActionResult> HandleAsync(
      Func<Guid, Task<IActionResult>> operation,
      CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var customerId)) return InvalidIdentity();

    try
    {
      return await operation(customerId);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      _logger.LogError(exception,
          "Operasi alamat Customer {CustomerId} gagal.", customerId);

      return StatusCode(StatusCodes.Status500InternalServerError, new
      {
        code = "ADDRESS_OPERATION_FAILED",
        message = "Permintaan alamat belum dapat diproses. Periksa hasilnya sebelum mencoba kembali."
      });
    }
  }

  private IActionResult ToResponse(
      CustomerAddressMutationResult result,
      string successCode,
      string successMessage,
      int successStatus = StatusCodes.Status200OK)
      => result.Status switch
      {
        CustomerAddressMutationStatus.Succeeded => StatusCode(successStatus, new
        {
          code = successCode,
          message = successMessage,
          data = result.Address
        }),
        CustomerAddressMutationStatus.CustomerNotFound => InvalidIdentity(),
        CustomerAddressMutationStatus.AddressNotFound => NotFound(new
        {
          code = "ADDRESS_NOT_FOUND",
          message = "Alamat tidak ditemukan."
        }),
        CustomerAddressMutationStatus.InvalidRegion => BadRequest(new
        {
          code = "ADDRESS_REGION_INVALID",
          message = "Kelurahan atau desa yang dipilih tidak tersedia.",
          errors = new { villageCode = new[] { "Pilih kembali kelurahan atau desa." } }
        }),
        CustomerAddressMutationStatus.RegionServiceUnavailable => StatusCode(
            StatusCodes.Status503ServiceUnavailable, new
            {
              code = "ADDRESS_REGION_UNAVAILABLE",
              message = "Wilayah alamat belum dapat diperiksa. Silakan coba kembali."
            }),
        _ => throw new InvalidOperationException("Status operasi alamat tidak dikenali.")
      };

  private IActionResult InvalidIdentity() => Unauthorized(new
  {
    code = "AUTH_INVALID_IDENTITY",
    message = "Identitas Customer tidak valid. Silakan login kembali."
  });
}
