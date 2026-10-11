using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class ProductOption
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;

    public ICollection<ProductOptionValue> Values { get; set; }
        = new List<ProductOptionValue>();
  }
}