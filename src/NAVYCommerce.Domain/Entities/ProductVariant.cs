namespace NAVYCommerce.Domain.Entities
{
  public class ProductVariant
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public Dictionary<string, string> VariantAttributes { get; set; } = new();
    public decimal PriceAdjustment { get; set; } = 0;
    public int StockQuantity { get; set; } = 0;
    public Product? Product { get; set; }
  }
}