using NAVYCommerce.API.Contracts.Addresses;

namespace NAVYCommerce.API.Services.Addresses
{
  public enum CustomerAddressMutationStatus
  {
    Succeeded,
    CustomerNotFound,
    AddressNotFound,
    InvalidRegion,
    RegionServiceUnavailable

  }

  public sealed record CustomerAddressMutationResult(
      CustomerAddressMutationStatus Status,
      CustomerAddressResponse? Address = null);
}