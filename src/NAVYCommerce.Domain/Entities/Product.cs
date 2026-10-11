using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class Product
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public int CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int WeightGrams { get; set; }
    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }
    public Store Store { get; set; } = null!;

    public Dictionary<string, List<string>> VariantOptions
    {
      get;
      set;
    } = new();

    public ICollection<ProductVariant> Variants { get; set; }
        = new List<ProductVariant>();

    public ICollection<ProductImage> Images { get; set; }
        = new List<ProductImage>();

    public ICollection<ProductOption> Options { get; set; }
        = new List<ProductOption>();
  }
}