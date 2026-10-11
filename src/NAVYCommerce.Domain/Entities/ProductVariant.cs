using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class ProductVariant
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public Dictionary<string, string> VariantAttributes
    {
      get;
      set;
    } = new();

    public decimal PriceAdjustment { get; set; }
    public int StockQuantity { get; set; }

    public Product? Product { get; set; }

    public ICollection<ProductVariantOptionValue> OptionValues
    {
      get;
      set;
    } = new List<ProductVariantOptionValue>();
  }
}