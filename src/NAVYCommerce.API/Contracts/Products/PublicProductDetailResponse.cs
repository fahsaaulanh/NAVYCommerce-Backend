using System;
using System.Collections.Generic;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed record PublicProductStoreResponse(
      Guid Id,
      string Name,
      string Slug);

  public sealed record PublicProductGalleryImageResponse(
      Guid Id,
      string Url,
      DateTimeOffset UrlExpiresAt,
      string AltText,
      int SortOrder,
      DateTime CreatedAt);

  public sealed record PublicProductDetailResponse(
      ProductDetailResponse Product,
      PublicProductStoreResponse Store,
      string? CategoryName,
      IReadOnlyList<PublicProductGalleryImageResponse> Images);
}