using System;

namespace NAVYCommerce.Domain.Entities
{
  public class ProductVariantOptionValue
  {
    public Guid ProductId { get; set; }

    public Guid VariantId { get; set; }

    public Guid OptionId { get; set; }

    public Guid OptionValueId { get; set; }

    public ProductVariant Variant { get; set; } = null!;

    public ProductOptionValue OptionValue { get; set; } = null!;
  }
}