using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVYCommerce.API.Contracts.Stores;
using NAVYCommerce.API.Services;

namespace NAVYCommerce.API.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize(Roles = "Seller")]
  public class StoresController : ControllerBase
  {
    private readonly IStoreService _storeService;

    public StoresController(IStoreService storeService)
    {
      _storeService = storeService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStoreRequest request,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      var result = await _storeService.CreateAsync(
          userId,
          request,
          cancellationToken);

      switch (result.Status)
      {
        case CreateStoreStatus.Created:
          return CreatedAtAction(
              nameof(GetById),
              new { id = result.Store!.Id },
              new
              {
                code = "STORE_CREATED",
                message = "Toko berhasil dibuat.",
                data = result.Store
              });

        case CreateStoreStatus.SlugAlreadyUsed:
          return Conflict(new
          {
            code = "STORE_SLUG_ALREADY_USED",
            message = "Slug toko sudah digunakan."
          });

        case CreateStoreStatus.OwnerNotEligible:
          return StatusCode(403, new
          {
            code = "STORE_OWNER_NOT_ELIGIBLE",
            message =
                  "Toko hanya dapat dibuat oleh seller terverifikasi."
          });

        default:
          throw new InvalidOperationException(
              "Hasil pembuatan toko tidak dikenali.");
      }
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] StoreListRequest request,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      var result = await _storeService.GetOwnedStoresAsync(
          userId,
          request,
          cancellationToken);

      Response.Headers["Cache-Control"] = "no-store";

      return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      var store = await _storeService.GetOwnedStoreAsync(
          userId,
          id,
          cancellationToken);

      if (store == null)
      {
        return NotFound(new
        {
          code = "STORE_NOT_FOUND",
          message = "Toko tidak ditemukan."
        });
      }

      Response.Headers["Cache-Control"] = "no-store";

      return Ok(new { data = store });
    }

    private bool TryGetUserId(out Guid userId)
    {
      var claim = User.FindFirstValue(
          ClaimTypes.NameIdentifier)
          ?? User.FindFirstValue("sub");

      return Guid.TryParse(claim, out userId);
    }
  }
}