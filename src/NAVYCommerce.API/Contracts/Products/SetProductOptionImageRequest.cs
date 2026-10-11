using System.Text.Json.Serialization;

namespace NAVYCommerce.API.Contracts.Products;

public sealed class SetProductOptionImageRequest
{
  [JsonRequired]
  public Guid? ImageId { get; set; }
}