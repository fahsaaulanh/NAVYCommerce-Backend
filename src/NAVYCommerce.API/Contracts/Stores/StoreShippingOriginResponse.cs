using System;

namespace NAVYCommerce.API.Contracts.Stores;

public sealed record StoreShippingOriginResponse(
    Guid StoreId,
    string ContactName,
    string PhoneNumber,
    string ProvinceCode,
    string Province,
    string CityCode,
    string City,
    string DistrictCode,
    string District,
    string VillageCode,
    string Village,
    string PostalCode,
    string AddressLine,
    string? PickupNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
