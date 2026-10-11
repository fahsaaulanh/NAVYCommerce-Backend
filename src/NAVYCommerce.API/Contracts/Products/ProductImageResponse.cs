namespace NAVYCommerce.API.Contracts.Products
{
  public sealed record ProductImageResponse(
      Guid Id,
      string Url,
      DateTimeOffset UrlExpiresAt,
      string AltText,
      int SortOrder,
      DateTime CreatedAt);
}