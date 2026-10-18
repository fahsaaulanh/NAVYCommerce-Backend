using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services.Shipping.Locations;

public sealed record ShippingRegion(
    string VillageCode, string Province, string City,
    string District, string Village, string PostalCode)
{
    public static ShippingRegion FromStoreOrigin(StoreShippingOrigin origin) =>
        new(origin.VillageCode, origin.Province, origin.City,
            origin.District, origin.Village, origin.PostalCode);

    public static ShippingRegion FromCustomerAddress(CustomerAddress address) =>
        new(address.VillageCode, address.Province, address.City,
            address.District, address.Village, address.PostalCode);
}
