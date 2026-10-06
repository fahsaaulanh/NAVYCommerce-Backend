using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Stores;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using Npgsql;

namespace NAVYCommerce.API.Services
{
  public enum CreateStoreStatus
  {
    Created,
    OwnerNotEligible,
    SlugAlreadyUsed
  }

  public sealed record CreateStoreResult(
      CreateStoreStatus Status,
      StoreResponse? Store = null);

  public interface IStoreService
  {
    Task<CreateStoreResult> CreateAsync(
        Guid ownerId,
        CreateStoreRequest request,
        CancellationToken cancellationToken = default);

    Task<StoreResponse?> GetOwnedStoreAsync(
        Guid ownerId,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<StoreListResponse> GetOwnedStoresAsync(
        Guid ownerId,
        StoreListRequest request,
        CancellationToken cancellationToken = default);
  }

  public class StoreService : IStoreService
  {
    private readonly ApplicationDbContext _context;

    public StoreService(ApplicationDbContext context)
    {
      _context = context;
    }

    public async Task<CreateStoreResult> CreateAsync(
        Guid ownerId,
        CreateStoreRequest request,
        CancellationToken cancellationToken = default)
    {
      var eligibleOwner = await _context.Users
          .AnyAsync(
              user =>
                  user.Id == ownerId &&
                  user.Role == UserRole.Seller &&
                  user.IsVerified,
              cancellationToken);

      if (!eligibleOwner)
      {
        return new(CreateStoreStatus.OwnerNotEligible);
      }

      var slugAlreadyUsed = await _context.Stores
          .AnyAsync(
              store => store.Slug == request.Slug,
              cancellationToken);

      if (slugAlreadyUsed)
      {
        return new(CreateStoreStatus.SlugAlreadyUsed);
      }

      var store = new Store
      {
        OwnerId = ownerId,
        Name = request.Name.Trim(),
        Slug = request.Slug,
        Description = string.IsNullOrWhiteSpace(
              request.Description)
                  ? null
                  : request.Description.Trim()
      };

      _context.Stores.Add(store);

      try
      {
        await _context.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException exception)
          when (exception.InnerException is PostgresException
          {
            SqlState: "23505",
            ConstraintName: "IX_stores_Slug"
          })
      {
        return new(CreateStoreStatus.SlugAlreadyUsed);
      }

      return new(
          CreateStoreStatus.Created,
          new StoreResponse(
              store.Id,
              store.Name,
              store.Slug,
              store.Description,
              store.CreatedAt));
    }

    public async Task<StoreResponse?> GetOwnedStoreAsync(
        Guid ownerId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
      return await _context.Stores
          .AsNoTracking()
          .Where(store =>
              store.Id == storeId &&
              store.OwnerId == ownerId &&
              store.Owner.Role == UserRole.Seller &&
              store.Owner.IsVerified)
          .Select(store => new StoreResponse(
              store.Id,
              store.Name,
              store.Slug,
              store.Description,
              store.CreatedAt))
          .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StoreListResponse> GetOwnedStoresAsync(
        Guid ownerId,
        StoreListRequest request,
        CancellationToken cancellationToken = default)
    {
      var query = _context.Stores
          .AsNoTracking()
          .Where(store =>
              store.OwnerId == ownerId &&
              store.Owner.Role == UserRole.Seller &&
              store.Owner.IsVerified);

      var total = await query.CountAsync(cancellationToken);

      var stores = await query
          .OrderByDescending(store => store.CreatedAt)
          .ThenBy(store => store.Id)
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .Select(store => new StoreResponse(
              store.Id,
              store.Name,
              store.Slug,
              store.Description,
              store.CreatedAt))
          .ToListAsync(cancellationToken);

      return new StoreListResponse(
          stores,
          request.Page,
          request.PageSize,
          total);
    }
  }
}