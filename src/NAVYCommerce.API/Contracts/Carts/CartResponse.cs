using System;
using System.Collections.Generic;
using NAVYCommerce.API.Contracts.Products;

namespace NAVYCommerce.API.Contracts.Carts
{
  public sealed record CartResponse(
      IReadOnlyList<CartItemResponse> Items,
      int TotalItems,
      long TotalQuantity,
      decimal Subtotal);

  public sealed record CartItemResponse(
      Guid Id,
      Guid ProductId,
      Guid ProductVariantId,
      Guid StoreId,
      string StoreName,
      string ProductTitle,
      string ProductSlug,
      string Sku,
      IReadOnlyList<CartItemOptionResponse> Options,
      int Quantity,
      decimal UnitPrice,
      decimal LineTotal,
      int AvailableStock,
      bool CanCheckout,
      ProductImageResponse? Image);

  public sealed record CartItemOptionResponse(
      string Name,
      string Value);
}