using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.Application.Abstractions.Imaging;

namespace NAVYCommerce.API.Services
{
  public enum ProductImageUploadStatus
  {
    Uploaded,
    ProductNotFound,
    ImageLimitReached,
    InvalidAltText,
    InvalidImage
  }

  public sealed record ProductImageUploadResult(
      ProductImageUploadStatus Status,
      ProductImageResponse? Image = null,
      ProductImageProcessingStatus? ProcessingStatus = null);

  public interface IProductImageService
  {
    Task<ProductImageUploadResult> UploadAsync(
        Guid ownerId,
        Guid productId,
        Stream content,
        string? altText,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductImageResponse>?>
        GetOwnedGalleryAsync(
            Guid ownerId,
            Guid productId,
            CancellationToken cancellationToken = default);

    Task<ProductImageReorderStatus> ReorderAsync(
        Guid ownerId,
        Guid productId,
        IReadOnlyList<Guid> imageIds,
        CancellationToken cancellationToken = default);

    Task<ProductOptionImageStatus> SetOptionImageAsync(
        Guid ownerId,
        Guid productId,
        Guid optionValueId,
        Guid? imageId,
        CancellationToken cancellationToken = default);

    Task<ProductImageAltTextStatus> UpdateAltTextAsync(
        Guid ownerId,
        Guid productId,
        Guid imageId,
        string? altText,
        CancellationToken cancellationToken = default);

    Task<ProductImageDeleteStatus> DeleteAsync(
        Guid ownerId,
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default);
  }
}