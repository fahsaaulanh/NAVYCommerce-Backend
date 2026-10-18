using System;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.API.Contracts.Stores;

namespace NAVYCommerce.API.Services.Stores.ShippingOrigins;

public enum StoreShippingOriginStatus
{
  Succeeded,
  StoreNotFound,
  InvalidRegion,
  RegionServiceUnavailable
}

public sealed record StoreShippingOriginResult(
    StoreShippingOriginStatus Status,
    StoreShippingOriginResponse? Origin = null);

public interface IStoreShippingOriginService
{
  Task<StoreShippingOriginResult> GetAsync(
      Guid ownerId, Guid storeId,
      CancellationToken cancellationToken = default);

  Task<StoreShippingOriginResult> SaveAsync(
      Guid ownerId, Guid storeId, SaveStoreShippingOriginRequest request,
      CancellationToken cancellationToken = default);
}
