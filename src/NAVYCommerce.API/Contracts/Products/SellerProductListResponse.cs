namespace NAVYCommerce.API.Contracts.Products;

public sealed record SellerProductThumbnailResponse(
    string Url,
    DateTimeOffset UrlExpiresAt,
    string AltText);

public sealed record SellerProductSummaryResponse(
    Guid Id,
    Guid StoreId,
    int CategoryId,
    string? CategoryName,
    string Title,
    string Slug,
    decimal BasePrice,
    bool IsActive,
    DateTime CreatedAt,
    int VariantCount,
    long TotalStock,
    int ImageCount,
    SellerProductThumbnailResponse? Thumbnail);

public sealed record SellerProductPaginationResponse(
    int Page,
    int PageSize,
    int Total,
    int TotalPages);

public sealed record SellerProductListResponse(
    IReadOnlyList<SellerProductSummaryResponse> Data,
    SellerProductPaginationResponse Pagination);