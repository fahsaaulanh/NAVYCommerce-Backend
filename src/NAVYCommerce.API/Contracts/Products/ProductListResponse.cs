using System;
using System.Collections.Generic;

namespace NAVYCommerce.API.Contracts.Products
{
  public record ProductSummaryResponse(
      Guid Id,
      Guid StoreId,
      int CategoryId,
      string Title,
      string Slug,
      decimal BasePrice);

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