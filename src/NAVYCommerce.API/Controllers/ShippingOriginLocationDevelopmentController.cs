using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Services.Shipping.Locations;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("shipping-development")]
[Route("api/development/shipping/origins/{storeId:guid}/location")]
public sealed class ShippingOriginLocationDevelopmentController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IShippingLocationService _locations;
    private readonly IHostEnvironment _environment;

    public ShippingOriginLocationDevelopmentController(ApplicationDbContext db,
        IShippingLocationService locations, IHostEnvironment environment)
    {
        _db = db;
        _locations = locations;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid storeId, [FromQuery, Range(0, 10000)] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var origin = await _db.Set<StoreShippingOrigin>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.StoreId == storeId, cancellationToken);
        if (origin is null)
            return Problem(statusCode: 404, title: "Shipping origin toko belum tersedia.");
        try
        {
            var region = ShippingRegion.FromStoreOrigin(origin);
            var id = await _locations.FindConfirmedIdAsync(region, cancellationToken);
            if (id.HasValue)
                return Ok(new { destinationId = id.Value, confirmed = true });

            var candidates = await _locations.FindCandidatesAsync(region, offset, cancellationToken);
            return Ok(new { confirmed = false, offset, limit = 50, region, candidates });
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 422, title: ex.Message);
        }
        catch (ShippingProviderException ex)
        {
            return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message);
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(Guid storeId,
        [FromBody] ConfirmShippingLocationRequest request, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var origin = await _db.Set<StoreShippingOrigin>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.StoreId == storeId, cancellationToken);
        if (origin is null)
            return Problem(statusCode: 404, title: "Shipping origin toko belum tersedia.");
        try
        {
            var id = await _locations.ConfirmAsync(ShippingRegion.FromStoreOrigin(origin),
                request.DestinationId, request.SearchOffset, cancellationToken);
            return Ok(new { destinationId = id, confirmed = true });
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 422, title: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 422, title: ex.Message);
        }
        catch (ShippingProviderException ex)
        {
            return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message);
        }
    }
}

public sealed class ConfirmShippingLocationRequest
{
    [Range(1, int.MaxValue)] public int DestinationId { get; init; }
    [Range(0, 10000)] public int SearchOffset { get; init; }
}
