using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NAVYCommerce.Application.Abstractions.Imaging
{
  public enum ProductImageProcessingStatus
  {
    Success,
    EmptyFile,
    FileTooLarge,
    InvalidImage,
    DimensionsTooLarge,
    AnimatedImageNotSupported
  }

  public sealed record ProcessedProductImage(
      byte[] Content,
      string ContentType,
      int Width,
      int Height);

  public sealed record ProductImageProcessingResult(
      ProductImageProcessingStatus Status,
      ProcessedProductImage? Image = null);

  public interface IProductImageProcessor
  {
    Task<ProductImageProcessingResult> ProcessAsync(
        Stream content,
        CancellationToken cancellationToken = default);
  }
}