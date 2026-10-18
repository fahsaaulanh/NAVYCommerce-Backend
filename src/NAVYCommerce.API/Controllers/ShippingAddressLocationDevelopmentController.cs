using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services.Shipping.Locations;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[EnableRateLimiting("shipping-development")]
[Route("api/development/shipping/addresses/{addressId:guid}/location")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShippingAddressLocationDevelopmentController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IShippingLocationService _locations;
    private readonly IHostEnvironment _environment;

    public ShippingAddressLocationDevelopmentController(ApplicationDbContext db,
        IShippingLocationService locations, IHostEnvironment environment)
    {
        _db = db; _locations = locations; _environment = environment;
    }

    private async Task<ShippingRegion?> ReadRegionAsync(Guid addressId, CancellationToken token)
    {
        if (!User.TryGetUserId(out var customerId)) return null;
        var address = await _db.Set<CustomerAddress>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == addressId && value.CustomerId == customerId &&
                _db.Users.Any(user => user.Id == customerId && user.IsVerified &&
                    user.Role == UserRole.Customer), token);
        return address == null ? null : ShippingRegion.FromCustomerAddress(address);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid addressId,
        [FromQuery, Range(0, 10000)] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var region = await ReadRegionAsync(addressId, cancellationToken);
        if (region == null) return NotFound(new { message = "Alamat tidak ditemukan." });
        try
        {
            var id = await _locations.FindConfirmedIdAsync(region, cancellationToken);
            if (id.HasValue) return Ok(new { destinationId = id.Value, confirmed = true });
            var candidates = await _locations.FindCandidatesAsync(region, offset, cancellationToken);
            return Ok(new { confirmed = false, offset, limit = 50, region, candidates });
        }
        catch (ArgumentException ex) { return Problem(statusCode: 422, title: ex.Message); }
        catch (ShippingProviderException ex) { return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message); }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(Guid addressId,
        [FromBody] ConfirmShippingLocationRequest request, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var region = await ReadRegionAsync(addressId, cancellationToken);
        if (region == null) return NotFound(new { message = "Alamat tidak ditemukan." });
        try
        {
            var id = await _locations.ConfirmAsync(region, request.DestinationId,
                request.SearchOffset, cancellationToken);
            return Ok(new { destinationId = id, confirmed = true });
        }
        catch (ArgumentException ex) { return Problem(statusCode: 422, title: ex.Message); }
        catch (InvalidOperationException ex) { return Problem(statusCode: 422, title: ex.Message); }
        catch (ShippingProviderException ex) { return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message); }
    }
}
