using System;

namespace NAVYCommerce.API.Services.Carts
{
  public enum CartMutationStatus
  {
    Succeeded,
    CustomerNotFound,
    VariantNotFound,
    ItemNotFound,
    ProductUnavailable,
    InsufficientStock,
    InvalidQuantity
  }

  public sealed record CartMutationResult(
      CartMutationStatus Status,
      Guid? ItemId = null,
      int? AvailableStock = null);
}