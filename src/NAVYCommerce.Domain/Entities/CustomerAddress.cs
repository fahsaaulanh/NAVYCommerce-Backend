using System;

namespace NAVYCommerce.Domain.Entities
{
  public class CustomerAddress
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public string Label { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string? Village { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string? DeliveryNotes { get; set; }

    public bool IsDefault { get; set; }

    public string ProvinceCode { get; set; } = string.Empty;

    public string CityCode { get; set; } = string.Empty;

    public string DistrictCode { get; set; } = string.Empty;

    public string VillageCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
  }
}