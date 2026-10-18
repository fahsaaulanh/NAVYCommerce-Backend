using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Authorize]
[Route("api/development/shipping")]
[EnableRateLimiting("shipping-development")]
public sealed class ShippingDevelopmentController : ControllerBase
{
    private readonly IRajaOngkirClient _client;
    private readonly IHostEnvironment _environment;

    public ShippingDevelopmentController(IRajaOngkirClient client, IHostEnvironment environment)
    {
        _client = client;
        _environment = environment;
    }

    [HttpGet("destinations")]
    public async Task<IActionResult> Search(
        [FromQuery, Required, StringLength(100, MinimumLength = 3)] string search,
        [FromQuery, Range(1, 50)] int limit = 20,
        [FromQuery, Range(0, 10000)] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        try
        {
            return Ok(await _client.SearchDestinationsAsync(search, limit, offset, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
        catch (ShippingProviderException ex)
        {
            return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message);
        }
    }

    [HttpPost("rates")]
    public async Task<IActionResult> Rates(
        [FromBody] ShippingRateDevelopmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        try
        {
            return Ok(await _client.GetRatesAsync(request.OriginId,
                request.DestinationId, request.WeightGrams, cancellationToken));
        }
        catch (ShippingProviderException ex)
        {
            return Problem(statusCode: ex.ResponseStatusCode, title: ex.Message);
        }
    }
}

public sealed class ShippingRateDevelopmentRequest
{
    [Range(1, int.MaxValue)] public int OriginId { get; init; }
    [Range(1, int.MaxValue)] public int DestinationId { get; init; }
    [Range(1, int.MaxValue)] public int WeightGrams { get; init; }
}
