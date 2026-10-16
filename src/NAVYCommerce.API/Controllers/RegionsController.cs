using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAVYCommerce.API.Contracts.Regions;
using NAVYCommerce.API.Services.Regions;

namespace NAVYCommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
public sealed class RegionsController : ControllerBase
{
  private readonly IRegionService _regions;

  public RegionsController(IRegionService regions)
  {
    _regions = regions;
  }

  [HttpGet("provinces")]
  public Task<IActionResult> GetProvinces(CancellationToken cancellationToken)
      => ReadAsync(() => _regions.GetProvincesAsync(cancellationToken));

  [HttpGet("cities")]
  public Task<IActionResult> GetCities(
      [FromQuery, Required(ErrorMessage = "Provinsi wajib dipilih.")]
      [RegularExpression(RegionCodePatterns.Province,
          ErrorMessage = "Format kode provinsi tidak valid.")]
      string provinceCode,
      CancellationToken cancellationToken)
      => ReadAsync(() => _regions.GetCitiesAsync(provinceCode, cancellationToken));

  [HttpGet("districts")]
  public Task<IActionResult> GetDistricts(
      [FromQuery, Required(ErrorMessage = "Kabupaten atau kota wajib dipilih.")]
      [RegularExpression(RegionCodePatterns.City,
          ErrorMessage = "Format kode kabupaten atau kota tidak valid.")]
      string cityCode,
      CancellationToken cancellationToken)
      => ReadAsync(() => _regions.GetDistrictsAsync(cityCode, cancellationToken));

  [HttpGet("villages")]
  public Task<IActionResult> GetVillages(
      [FromQuery, Required(ErrorMessage = "Kecamatan wajib dipilih.")]
      [RegularExpression(RegionCodePatterns.District,
          ErrorMessage = "Format kode kecamatan tidak valid.")]
      string districtCode,
      CancellationToken cancellationToken)
      => ReadAsync(() => _regions.GetVillagesAsync(districtCode, cancellationToken));

  private async Task<IActionResult> ReadAsync(
      Func<Task<IReadOnlyList<RegionResponse>>> operation)
  {
    try
    {
      var data = await operation();
      return Ok(new { data });
    }
    catch (RegionServiceUnavailableException)
    {
      return StatusCode(StatusCodes.Status503ServiceUnavailable, new
      {
        code = "REGION_PROVIDER_UNAVAILABLE",
        message = "Data wilayah belum dapat dimuat. Silakan coba kembali."
      });
    }
  }
}
