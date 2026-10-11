namespace NAVYCommerce.Infrastructure.Storage
{
  public sealed class ProductImageStorageOptions
  {
    public const string SectionName = "ProductImageStorage";

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "product-images";

    public string PublicServiceUrl { get; set; } = string.Empty;

    public int ReadUrlMinutes { get; set; } = 30;

    public int MaxFileBytes { get; set; } = 5 * 1024 * 1024;

    public bool AllowHttp { get; set; }
  }
}