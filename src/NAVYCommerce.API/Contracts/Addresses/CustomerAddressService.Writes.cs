using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Addresses;
using NAVYCommerce.API.Services.Regions;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services.Addresses;

public sealed partial class CustomerAddressService
{
  public Task<CustomerAddressMutationResult> CreateAsync(
      Guid customerId,
      SaveCustomerAddressRequest request,
      CancellationToken cancellationToken = default)
      => SaveAsync(customerId, null, request, cancellationToken);

  public Task<CustomerAddressMutationResult> UpdateAsync(
      Guid customerId,
      Guid addressId,
      SaveCustomerAddressRequest request,
      CancellationToken cancellationToken = default)
      => SaveAsync(customerId, addressId, request, cancellationToken);

  private async Task<CustomerAddressMutationResult> SaveAsync(
      Guid customerId,
      Guid? addressId,
      SaveCustomerAddressRequest request,
      CancellationToken cancellationToken)
  {
    if (!await IsCustomerAsync(customerId, cancellationToken))
      return new(CustomerAddressMutationStatus.CustomerNotFound);


    if (addressId.HasValue && !await OwnedAddresses(customerId)
        .AnyAsync(address => address.Id == addressId.Value, cancellationToken))
      return new(CustomerAddressMutationStatus.AddressNotFound);

    ResolvedRegion? region;
    try
    {
      region = await _regions.ResolveVillageAsync(request.VillageCode, cancellationToken);
    }
    catch (RegionServiceUnavailableException)
    {
      return new(CustomerAddressMutationStatus.RegionServiceUnavailable);
    }

    if (region == null) return new(CustomerAddressMutationStatus.InvalidRegion);

    return await MutateAsync(customerId, async token =>
    {
      CustomerAddress address;
      var now = DateTime.UtcNow;

      if (addressId.HasValue)
      {
        var existing = await OwnedAddresses(customerId)
            .SingleOrDefaultAsync(value => value.Id == addressId.Value, token);
        if (existing == null)
          return new(CustomerAddressMutationStatus.AddressNotFound);
        address = existing;
      }
      else
      {
        address = new CustomerAddress
        {
          CustomerId = customerId,
          CreatedAt = now
        };
        _context.CustomerAddresses.Add(address);
      }

      Apply(address, request, region, now);

      if (!address.IsDefault && !await OwnedAddresses(customerId)
          .AnyAsync(value => value.IsDefault, token))
        address.IsDefault = true;

      return new(CustomerAddressMutationStatus.Succeeded, Map(address));
    }, cancellationToken);
  }

  public Task<CustomerAddressMutationResult> SetDefaultAsync(
      Guid customerId,
      Guid addressId,
      CancellationToken cancellationToken = default)
      => MutateAsync(customerId, async token =>
      {
        var address = await OwnedAddresses(customerId)
            .SingleOrDefaultAsync(value => value.Id == addressId, token);
        if (address == null)
          return new(CustomerAddressMutationStatus.AddressNotFound);

        if (address.IsDefault)
          return new(CustomerAddressMutationStatus.Succeeded, Map(address));

        var previous = await OwnedAddresses(customerId)
            .SingleOrDefaultAsync(value => value.IsDefault, token);
        var now = DateTime.UtcNow;

        if (previous != null)
        {
          previous.IsDefault = false;
          previous.UpdatedAt = now;

          await _context.SaveChangesAsync(token);
        }

        address.IsDefault = true;
        address.UpdatedAt = now;
        return new(CustomerAddressMutationStatus.Succeeded, Map(address));
      }, cancellationToken);

  public Task<CustomerAddressMutationResult> DeleteAsync(
      Guid customerId,
      Guid addressId,
      CancellationToken cancellationToken = default)
      => MutateAsync(customerId, async token =>
      {
        var address = await OwnedAddresses(customerId)
            .SingleOrDefaultAsync(value => value.Id == addressId, token);
        if (address == null)
          return new(CustomerAddressMutationStatus.AddressNotFound);

        var wasDefault = address.IsDefault;
        _context.CustomerAddresses.Remove(address);

        if (wasDefault)
        {
          await _context.SaveChangesAsync(token);

          var replacement = await OwnedAddresses(customerId)
              .OrderBy(value => value.CreatedAt)
              .ThenBy(value => value.Id)
              .FirstOrDefaultAsync(token);

          if (replacement != null)
          {
            replacement.IsDefault = true;
            replacement.UpdatedAt = DateTime.UtcNow;
          }
        }

        return new(CustomerAddressMutationStatus.Succeeded);
      }, cancellationToken);
}
