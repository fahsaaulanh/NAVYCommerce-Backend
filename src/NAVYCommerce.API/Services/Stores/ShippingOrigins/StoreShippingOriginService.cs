using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Stores;
using NAVYCommerce.API.Services.Regions;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Services.Stores.ShippingOrigins;

public sealed class StoreShippingOriginService : IStoreShippingOriginService
{
  private readonly ApplicationDbContext _context;
  private readonly IRegionService _regions;

  public StoreShippingOriginService(ApplicationDbContext context, IRegionService regions)
  {
    _context = context;
    _regions = regions;
  }

  private IQueryable<Store> OwnedStores(Guid ownerId, Guid storeId)
      => _context.Stores.Where(store => store.Id == storeId &&
          store.OwnerId == ownerId && store.Owner.IsVerified &&
          store.Owner.Role == UserRole.Seller);

  public async Task<StoreShippingOriginResult> GetAsync(
      Guid ownerId, Guid storeId,
      CancellationToken cancellationToken = default)
  {
    if (ownerId == Guid.Empty || storeId == Guid.Empty)
      return new(StoreShippingOriginStatus.StoreNotFound);


    var store = await OwnedStores(ownerId, storeId)
        .AsNoTracking()
        .Select(value => new { Origin = value.ShippingOrigin })
        .SingleOrDefaultAsync(cancellationToken);

    if (store == null) return new(StoreShippingOriginStatus.StoreNotFound);

    return new(StoreShippingOriginStatus.Succeeded,
        store.Origin == null ? null : Map(store.Origin));
  }

  public async Task<StoreShippingOriginResult> SaveAsync(
      Guid ownerId, Guid storeId, SaveStoreShippingOriginRequest request,
      CancellationToken cancellationToken = default)
  {
    if (ownerId == Guid.Empty || storeId == Guid.Empty ||
        !await OwnedStores(ownerId, storeId).AnyAsync(cancellationToken))
      return new(StoreShippingOriginStatus.StoreNotFound);

    ResolvedRegion? region;
    try
    {
      // Tidak memegang lock database selama menunggu HTTP/cache wilayah.
      region = await _regions.ResolveVillageAsync(request.VillageCode, cancellationToken);
    }
    catch (RegionServiceUnavailableException)
    {
      return new(StoreShippingOriginStatus.RegionServiceUnavailable);
    }
    if (region == null) return new(StoreShippingOriginStatus.InvalidRegion);

    await using var transaction =
        await _context.Database.BeginTransactionAsync(cancellationToken);

    var rows = await _context.Stores
        .FromSqlInterpolated($"SELECT * FROM stores WHERE \"Id\" = {storeId} FOR UPDATE")
        .AsNoTracking()
        .ToListAsync(cancellationToken);
    var lockedStore = rows.SingleOrDefault();

    if (lockedStore == null || lockedStore.OwnerId != ownerId ||
        !await OwnedStores(ownerId, storeId).AnyAsync(cancellationToken))
      return new(StoreShippingOriginStatus.StoreNotFound);

    var origin = await _context.StoreShippingOrigins
        .SingleOrDefaultAsync(value => value.StoreId == storeId, cancellationToken);
    var now = DateTime.UtcNow;
    if (origin == null)
    {
      origin = new StoreShippingOrigin { StoreId = storeId, CreatedAt = now };
      _context.StoreShippingOrigins.Add(origin);
    }

    origin.ContactName = request.ContactName.Trim();
    origin.PhoneNumber = request.PhoneNumber.Trim();
    origin.ProvinceCode = region.Province.Code;
    origin.Province = region.Province.Name;
    origin.CityCode = region.City.Code;
    origin.City = region.City.Name;
    origin.DistrictCode = region.District.Code;
    origin.District = region.District.Name;
    origin.VillageCode = region.Village.Code;
    origin.Village = region.Village.Name;
    origin.PostalCode = request.PostalCode.Trim();
    origin.AddressLine = request.AddressLine.Trim();
    origin.PickupNotes = string.IsNullOrWhiteSpace(request.PickupNotes)
        ? null : request.PickupNotes.Trim();
    origin.UpdatedAt = now;

    await _context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new(StoreShippingOriginStatus.Succeeded, Map(origin));
  }

  private static StoreShippingOriginResponse Map(StoreShippingOrigin origin)
      => new(
          origin.StoreId, origin.ContactName, origin.PhoneNumber,
          origin.ProvinceCode, origin.Province,
          origin.CityCode, origin.City,
          origin.DistrictCode, origin.District,
          origin.VillageCode, origin.Village,
          origin.PostalCode, origin.AddressLine, origin.PickupNotes,
          origin.CreatedAt, origin.UpdatedAt);
}
