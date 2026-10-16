using System;

namespace NAVYCommerce.Domain.Entities
{
  public class CartItem
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CartId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Cart Cart { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
  }
}