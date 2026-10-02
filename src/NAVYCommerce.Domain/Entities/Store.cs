using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class Store
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
  }
}