using System;
using System.Collections.Generic;

namespace NAVYCommerce.API.Contracts.Products
{
    public record ProductOptionValueResponse(
        Guid Id,
        string Value,
        int SortOrder,
        Guid? ImageId);

    public record ProductOptionResponse(
        Guid Id,
        string Name,
        int SortOrder,
        IReadOnlyList<ProductOptionValueResponse> Values);

    public record ProductVariantResponse(
        Guid Id,
        string Sku,
        IReadOnlyList<Guid> OptionValueIds,
        decimal PriceAdjustment,
        decimal Price,
        int StockQuantity);

    public record ProductDetailResponse(
        Guid Id,
        Guid StoreId,
        int CategoryId,
        string Title,
        string Slug,
        string? Description,
        int WeightGrams,
        decimal BasePrice,
        bool IsActive,
        DateTime CreatedAt,
        IReadOnlyList<ProductOptionResponse> Options,
        IReadOnlyList<ProductVariantResponse> Variants);
}