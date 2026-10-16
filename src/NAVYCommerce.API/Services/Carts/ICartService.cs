using System;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.API.Contracts.Carts;

namespace NAVYCommerce.API.Services.Carts;

public interface ICartService
{
  Task<CartResponse?> GetAsync(Guid customerId, CancellationToken cancellationToken = default);
  Task<CartSummaryResponse?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);
  Task<CartMutationResult> AddItemAsync(Guid customerId, Guid productVariantId, int quantity, CancellationToken cancellationToken = default);
  Task<CartMutationResult> UpdateItemQuantityAsync(Guid customerId, Guid cartItemId, int quantity, CancellationToken cancellationToken = default);
  Task<CartMutationResult> RemoveItemAsync(Guid customerId, Guid cartItemId, CancellationToken cancellationToken = default);
}
