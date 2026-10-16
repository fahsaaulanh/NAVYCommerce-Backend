using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.API.Contracts.Addresses;

namespace NAVYCommerce.API.Services.Addresses
{
  public interface ICustomerAddressService
  {
    Task<IReadOnlyList<CustomerAddressResponse>?> GetMineAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressMutationResult> CreateAsync(
        Guid customerId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressMutationResult> UpdateAsync(
        Guid customerId,
        Guid addressId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressMutationResult> DeleteAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressMutationResult> SetDefaultAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default);
  }
}