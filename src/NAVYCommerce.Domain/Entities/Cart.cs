using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class Cart
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; }
        = new List<CartItem>();
  }
}