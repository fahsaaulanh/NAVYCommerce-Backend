using System;

namespace NAVYCommerce.API.Contracts.Addresses
{
  public sealed record CustomerAddressResponse(
      Guid Id,
      string Label,
      string RecipientName,
      string PhoneNumber,
      string ProvinceCode,
      string Province,
      string CityCode,
      string City,
      string DistrictCode,
      string District,
      string VillageCode,
      string? Village,
      string PostalCode,
      string AddressLine,
      string? DeliveryNotes,
      bool IsDefault,
      DateTime CreatedAt,
      DateTime UpdatedAt);
}