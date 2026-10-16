using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Carts;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services.Carts
{
    public sealed partial class CartService
    {
        public async Task<CartSummaryResponse?> GetSummaryAsync(
            Guid customerId,
            CancellationToken cancellationToken = default)
        {
            if (!await IsCustomerAsync(customerId, cancellationToken)) return null;

            var summary = await _context.CartItems
                .Where(item => item.Cart.CustomerId == customerId)
                .GroupBy(item => item.CartId)
                .Select(items => new CartSummaryResponse(
                    items.Count(), items.Sum(item => (long)item.Quantity)))
                .SingleOrDefaultAsync(cancellationToken);

            return summary ?? new CartSummaryResponse(0, 0);
        }

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
    }
}
