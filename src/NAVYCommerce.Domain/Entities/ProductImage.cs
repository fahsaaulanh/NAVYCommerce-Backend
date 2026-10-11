using System;

namespace NAVYCommerce.Domain.Entities
{
  public class ProductImage
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
  }
}