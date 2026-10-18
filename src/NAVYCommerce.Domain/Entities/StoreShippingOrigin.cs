using System;

namespace NAVYCommerce.Domain.Entities;

public class StoreShippingOrigin
{
  public Guid StoreId { get; set; }
  public string ContactName { get; set; } = string.Empty;
  public string PhoneNumber { get; set; } = string.Empty;
  public string ProvinceCode { get; set; } = string.Empty;
  public string Province { get; set; } = string.Empty;
  public string CityCode { get; set; } = string.Empty;
  public string City { get; set; } = string.Empty;
  public string DistrictCode { get; set; } = string.Empty;
  public string District { get; set; } = string.Empty;
  public string VillageCode { get; set; } = string.Empty;
  public string Village { get; set; } = string.Empty;
  public string PostalCode { get; set; } = string.Empty;
  public string AddressLine { get; set; } = string.Empty;
  public string? PickupNotes { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
  public Store Store { get; set; } = null!;
}
