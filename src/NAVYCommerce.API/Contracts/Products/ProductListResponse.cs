using System;
using System.Collections.Generic;

namespace NAVYCommerce.API.Contracts.Products
{
    public sealed record PublicProductThumbnailResponse(
        string Url,
        DateTimeOffset UrlExpiresAt,
        string AltText);

    public record ProductSummaryResponse(
        Guid Id,
        Guid StoreId,
        int CategoryId,
        string Title,
        string Slug,
        decimal BasePrice,
        string StoreName,
        string StoreSlug,
        string? CategoryName,
        decimal MinPrice,
        decimal MaxPrice,
        bool HasStock,
        PublicProductThumbnailResponse? Thumbnail);

    public record ProductListResponse(
        IReadOnlyList<ProductSummaryResponse> Data,
        int Page,
        int PageSize,
        int Total)
    {
        public int TotalPages =>
            (int)Math.Ceiling((double)Total / PageSize);
    }
}