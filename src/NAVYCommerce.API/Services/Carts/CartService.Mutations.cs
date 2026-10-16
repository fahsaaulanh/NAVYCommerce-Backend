using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services.Carts
{
  public sealed partial class CartService
  {
    public Task<CartMutationResult> AddItemAsync(
        Guid customerId,
        Guid productVariantId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
      if (quantity < 1)
      {
        return Task.FromResult(
            new CartMutationResult(CartMutationStatus.InvalidQuantity));
      }

      return ExecuteMutationAsync(customerId, async token =>
      {
        var variant = await _context.ProductVariants
            .AsNoTracking()
            .Include(item => item.Product!)
                .ThenInclude(product => product.Store)
                .ThenInclude(store => store.Owner)
            .SingleOrDefaultAsync(
                item => item.Id == productVariantId,
                token);

        if (variant == null)
        {
          return new CartMutationResult(
              CartMutationStatus.VariantNotFound);
        }

        var cart = await _context.Carts.SingleOrDefaultAsync(
            item => item.CustomerId == customerId,
            token);

        var existingItem = cart == null
            ? null
            : await _context.CartItems.SingleOrDefaultAsync(
                item => item.CartId == cart.Id &&
                    item.ProductVariantId == productVariantId,
                token);

        var nextQuantity = (long)(existingItem?.Quantity ?? 0) + quantity;
        var failure = ValidateVariant(variant, nextQuantity);

        if (failure != null)
        {
          return failure;
        }

        var now = DateTime.UtcNow;

        if (cart == null)
        {
          cart = new Cart
          {
            CustomerId = customerId,
            CreatedAt = now,
            UpdatedAt = now
          };

          _context.Carts.Add(cart);
        }

        if (existingItem == null)
        {
          existingItem = new CartItem
          {
            CartId = cart.Id,
            Cart = cart,
            ProductVariantId = productVariantId,
            Quantity = (int)nextQuantity,
            CreatedAt = now,
            UpdatedAt = now
          };

          _context.CartItems.Add(existingItem);
        }
        else
        {
          existingItem.Quantity = (int)nextQuantity;
          existingItem.UpdatedAt = now;
        }

        cart.UpdatedAt = now;

        return new CartMutationResult(
            CartMutationStatus.Succeeded,
            existingItem.Id);
      }, cancellationToken);
    }

    public Task<CartMutationResult> UpdateItemQuantityAsync(
        Guid customerId,
        Guid cartItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
      if (quantity < 1)
      {
        return Task.FromResult(
            new CartMutationResult(CartMutationStatus.InvalidQuantity));
      }

      return ExecuteMutationAsync(customerId, async token =>
      {
        var item = await _context.CartItems
            .Include(candidate => candidate.Cart)
            .Include(candidate => candidate.ProductVariant)
                .ThenInclude(variant => variant.Product!)
                .ThenInclude(product => product.Store)
                .ThenInclude(store => store.Owner)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == cartItemId &&
                    candidate.Cart.CustomerId == customerId,
                token);

        if (item == null)
        {
          return new CartMutationResult(CartMutationStatus.ItemNotFound);
        }

        var failure = ValidateVariant(item.ProductVariant, quantity);

        if (failure != null)
        {
          return failure;
        }

        var now = DateTime.UtcNow;
        item.Quantity = quantity;
        item.UpdatedAt = now;
        item.Cart.UpdatedAt = now;

        return new CartMutationResult(
            CartMutationStatus.Succeeded,
            item.Id);
      }, cancellationToken);
    }

    public Task<CartMutationResult> RemoveItemAsync(
        Guid customerId,
        Guid cartItemId,
        CancellationToken cancellationToken = default)
    {
      return ExecuteMutationAsync(customerId, async token =>
      {
        var item = await _context.CartItems
            .Include(candidate => candidate.Cart)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == cartItemId &&
                    candidate.Cart.CustomerId == customerId,
                token);

        if (item == null)
        {
          return new CartMutationResult(CartMutationStatus.ItemNotFound);
        }

        item.Cart.UpdatedAt = DateTime.UtcNow;
        _context.CartItems.Remove(item);

        return new CartMutationResult(
            CartMutationStatus.Succeeded,
            item.Id);
      }, cancellationToken);
    }

    private async Task<CartMutationResult> ExecuteMutationAsync(
        Guid customerId,
        Func<CancellationToken, Task<CartMutationResult>> mutation,
        CancellationToken cancellationToken)
    {
      if (customerId == Guid.Empty)
      {
        return new(CartMutationStatus.CustomerNotFound);
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(cancellationToken);

      var customers = await _context.Users
          .FromSqlInterpolated(
              $"SELECT * FROM users WHERE \"Id\" = {customerId} FOR UPDATE")
          .AsNoTracking()
          .ToListAsync(cancellationToken);

      var customer = customers.SingleOrDefault();

      if (customer == null ||
          customer.Role != UserRole.Customer ||
          !customer.IsVerified)
      {
        return new(CartMutationStatus.CustomerNotFound);
      }

      var result = await mutation(cancellationToken);

      if (result.Status != CartMutationStatus.Succeeded)
      {
        return result;
      }

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return result;
    }
  }
}
