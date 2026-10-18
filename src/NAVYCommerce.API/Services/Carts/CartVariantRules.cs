using NAVYCommerce.Domain.Entities;
using NAVYCommerce.API.Contracts.Carts;

namespace NAVYCommerce.API.Services.Carts;

// Aturan yang sama untuk keranjang dan preview ongkir checkout.
// Pemanggil harus memuat Product -> Store -> Owner terlebih dahulu.
internal static class CartVariantRules
{
    internal static CartMutationResult? Validate(ProductVariant variant, long quantity)
    {
        if (quantity < 1 || quantity > int.MaxValue)
            return new(CartMutationStatus.InvalidQuantity);

        var product = variant.Product;
        if (product == null || !product.IsActive ||
            !product.Store.Owner.IsVerified ||
            product.Store.Owner.Role != UserRole.Seller ||
            product.BasePrice + variant.PriceAdjustment <= 0)
            return new(CartMutationStatus.ProductUnavailable);

        if (quantity > variant.StockQuantity)
            return new(CartMutationStatus.InsufficientStock,
                AvailableStock: variant.StockQuantity);

        return null;
    }
}
