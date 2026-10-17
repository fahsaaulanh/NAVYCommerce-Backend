using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Addresses;
using NAVYCommerce.API.Services.Regions;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Services.Addresses;

public sealed partial class CustomerAddressService : ICustomerAddressService
{
    private readonly ApplicationDbContext _context;
    private readonly IRegionService _regions;

    public CustomerAddressService(
        ApplicationDbContext context,
        IRegionService regions)
    {
        _context = context;
        _regions = regions;
    }

    public async Task<IReadOnlyList<CustomerAddressResponse>?> GetMineAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCustomerAsync(customerId, cancellationToken)) return null;

        var addresses = await OwnedAddresses(customerId)
            .AsNoTracking()
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.UpdatedAt)
            .ThenBy(address => address.Id)
            .ToListAsync(cancellationToken);

        return addresses.Select(Map).ToArray();
    }

    private IQueryable<CustomerAddress> OwnedAddresses(Guid customerId)
        => _context.CustomerAddresses.Where(address => address.CustomerId == customerId);

    private Task<bool> IsCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) return Task.FromResult(false);

        return _context.Users.AnyAsync(
            user => user.Id == customerId &&
                user.Role == UserRole.Customer && user.IsVerified,
            cancellationToken);
    }

    private async Task<CustomerAddressMutationResult> MutateAsync(
        Guid customerId,
        Func<CancellationToken, Task<CustomerAddressMutationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
            return new(CustomerAddressMutationStatus.CustomerNotFound);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);


        var users = await _context.Users
            .FromSqlInterpolated(
                $"SELECT * FROM users WHERE \"Id\" = {customerId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var customer = users.SingleOrDefault();
        if (customer == null || customer.Role != UserRole.Customer || !customer.IsVerified)
            return new(CustomerAddressMutationStatus.CustomerNotFound);

        var result = await operation(cancellationToken);
        if (result.Status != CustomerAddressMutationStatus.Succeeded) return result;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static CustomerAddressResponse Map(CustomerAddress address)
        => new(
            address.Id,
            address.Label,
            address.RecipientName,
            address.PhoneNumber,
            address.ProvinceCode,
            address.Province,
            address.CityCode,
            address.City,
            address.DistrictCode,
            address.District,
            address.VillageCode,
            address.Village,
            address.PostalCode,
            address.AddressLine,
            address.DeliveryNotes,
            address.IsDefault,
            address.CreatedAt,
            address.UpdatedAt);

    private static void Apply(
        CustomerAddress address,
        SaveCustomerAddressRequest request,
        ResolvedRegion region,
        DateTime now)
    {
        address.Label = request.Label.Trim();
        address.RecipientName = request.RecipientName.Trim();
        address.PhoneNumber = request.PhoneNumber.Trim();
        address.ProvinceCode = region.Province.Code;
        address.Province = region.Province.Name;
        address.CityCode = region.City.Code;
        address.City = region.City.Name;
        address.DistrictCode = region.District.Code;
        address.District = region.District.Name;
        address.VillageCode = region.Village.Code;
        address.Village = region.Village.Name;
        address.PostalCode = request.PostalCode.Trim();
        address.AddressLine = request.AddressLine.Trim();
        address.DeliveryNotes = string.IsNullOrWhiteSpace(request.DeliveryNotes)
            ? null : request.DeliveryNotes.Trim();
        address.UpdatedAt = now;
    }
}
