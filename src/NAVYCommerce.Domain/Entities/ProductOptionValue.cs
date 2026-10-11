using System;

namespace NAVYCommerce.Domain.Entities
{
  public class ProductOptionValue
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Guid OptionId { get; set; }

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Guid? ImageId { get; set; }

    public ProductOption Option { get; set; } = null!;

    public ProductImage? Image { get; set; }
  }
}