using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.Application.Abstractions.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace NAVYCommerce.Infrastructure.Imaging
{
  public sealed class ImageSharpProductImageProcessor
      : IProductImageProcessor
  {
    private const int MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxInputDimension = 6000;
    private const long MaxInputPixels = 16_000_000;
    private const int MaxOutputDimension = 2000;

    private readonly Configuration _configuration = new(
        new JpegConfigurationModule(),
        new PngConfigurationModule(),
        new WebpConfigurationModule());

    public async Task<ProductImageProcessingResult> ProcessAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(content);

      if (!content.CanRead)
      {
        return new(
            ProductImageProcessingStatus.InvalidImage);
      }

      using var input = new MemoryStream();

      var buffer = new byte[81920];
      int bytesRead;

      while ((bytesRead = await content.ReadAsync(
          buffer.AsMemory(),
          cancellationToken)) > 0)
      {
        if (input.Length + bytesRead > MaxFileBytes)
        {
          return new(
              ProductImageProcessingStatus.FileTooLarge);
        }

        await input.WriteAsync(
            buffer.AsMemory(0, bytesRead),
            cancellationToken);
      }

      if (input.Length == 0)
      {
        return new(
            ProductImageProcessingStatus.EmptyFile);
      }

      input.Position = 0;

      var decoderOptions = new DecoderOptions
      {
        Configuration = _configuration,

        MaxFrames = 2
      };

      try
      {
        var info = await Image.IdentifyAsync(
            decoderOptions,
            input,
            cancellationToken);

        if (info.Width <= 0 || info.Height <= 0)
        {
          return new(
              ProductImageProcessingStatus.InvalidImage);
        }

        if (info.Width > MaxInputDimension
            || info.Height > MaxInputDimension
            || (long)info.Width * info.Height > MaxInputPixels)
        {
          return new(
              ProductImageProcessingStatus.DimensionsTooLarge);
        }

        input.Position = 0;

        using var image = await Image.LoadAsync(
            decoderOptions,
            input,
            cancellationToken);

        if (image.Frames.Count > 1)
        {
          return new(
              ProductImageProcessingStatus
                  .AnimatedImageNotSupported);
        }

        image.Mutate(context => context.AutoOrient());

        if (image.Width > MaxOutputDimension
            || image.Height > MaxOutputDimension)
        {
          image.Mutate(context => context.Resize(
              new ResizeOptions
              {
                Size = new Size(
                      MaxOutputDimension,
                      MaxOutputDimension),
                Mode = ResizeMode.Max
              }));
        }

        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        cancellationToken.ThrowIfCancellationRequested();

        using var output = new MemoryStream();

        await image.SaveAsync(
            output,
            new WebpEncoder
            {
              Quality = 85
            },
            cancellationToken);

        if (output.Length > MaxFileBytes)
        {
          return new(
              ProductImageProcessingStatus.FileTooLarge);
        }

        return new(
            ProductImageProcessingStatus.Success,
            new ProcessedProductImage(
                output.ToArray(),
                "image/webp",
                image.Width,
                image.Height));
      }
      catch (UnknownImageFormatException)
      {
        return new(
            ProductImageProcessingStatus.InvalidImage);
      }
      catch (InvalidImageContentException)
      {
        return new(
            ProductImageProcessingStatus.InvalidImage);
      }
    }
  }
}