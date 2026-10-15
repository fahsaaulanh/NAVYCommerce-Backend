using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.API.Services;
using NAVYCommerce.Application.Abstractions.Imaging;

namespace NAVYCommerce.API.Controllers
{
  [ApiController]
  [Authorize(Roles = "Seller")]
  [Route("api/Products/{productId:guid}/images")]
  public sealed class ProductImagesController : ControllerBase
  {
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private const long MaxRequestBytes = 6 * 1024 * 1024;

    private readonly IProductImageService _imageService;
    private readonly ILogger<ProductImagesController> _logger;

    public ProductImagesController(
        IProductImageService imageService,
        ILogger<ProductImagesController> logger)
    {
      _imageService = imageService;
      _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(
        Guid productId,
        [FromForm] UploadProductImageRequest request,
        CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return ErrorResponse(
            StatusCodes.Status401Unauthorized,
            "AUTH_INVALID_IDENTITY",
            "Identitas user tidak valid.");
      }

      if (Request.Form.Files.Count != 1)
      {
        return ErrorResponse(
            StatusCodes.Status400BadRequest,
            "PRODUCT_IMAGE_FILE_COUNT_INVALID",
            "Unggah tepat satu gambar per permintaan.");
      }

      if (request.File.Length == 0)
      {
        return ErrorResponse(
            StatusCodes.Status400BadRequest,
            "PRODUCT_IMAGE_EMPTY",
            "File gambar tidak boleh kosong.");
      }

      if (request.File.Length > MaxFileBytes)
      {
        return ErrorResponse(
            StatusCodes.Status413PayloadTooLarge,
            "PRODUCT_IMAGE_TOO_LARGE",
            "Ukuran gambar maksimal 5 MiB.");
      }

      try
      {
        using var content = request.File.OpenReadStream();

        var result = await _imageService.UploadAsync(
            userId,
            productId,
            content,
            request.AltText,
            cancellationToken);

        switch (result.Status)
        {
          case ProductImageUploadStatus.Uploaded:
            if (result.Image == null)
            {
              throw new InvalidOperationException(
                  "Response gambar tidak tersedia.");
            }

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                  code = "PRODUCT_IMAGE_UPLOADED",
                  message = "Gambar berhasil diunggah.",
                  data = result.Image
                });

          case ProductImageUploadStatus.ProductNotFound:
            return ErrorResponse(
                StatusCodes.Status404NotFound,
                "PRODUCT_NOT_FOUND",
                "Produk tidak ditemukan.");

          case ProductImageUploadStatus.ImageLimitReached:
            return ErrorResponse(
                StatusCodes.Status409Conflict,
                "PRODUCT_IMAGE_LIMIT_REACHED",
                "Galeri produk maksimal 10 gambar.");

          case ProductImageUploadStatus.InvalidAltText:
            return ErrorResponse(
                StatusCodes.Status400BadRequest,
                "PRODUCT_IMAGE_ALT_TEXT_INVALID",
                "Teks alternatif maksimal 250 karakter.");

          case ProductImageUploadStatus.InvalidImage:
            return ProcessingError(
                result.ProcessingStatus);

          default:
            throw new InvalidOperationException(
                "Status upload gambar tidak dikenal.");
        }
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Upload gambar gagal untuk produk {ProductId}.",
            productId);

        return ErrorResponse(
            StatusCodes.Status500InternalServerError,
            "PRODUCT_IMAGE_UPLOAD_FAILED",
            "Gambar belum dapat diunggah. Silakan coba kembali.");
      }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(
    StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGallery(
    Guid productId,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return ErrorResponse(
            StatusCodes.Status401Unauthorized,
            "AUTH_INVALID_IDENTITY",
            "Identitas user tidak valid.");
      }

      try
      {
        var gallery = await _imageService.GetOwnedGalleryAsync(
            userId,
            productId,
            cancellationToken);

        if (gallery == null)
        {
          return ErrorResponse(
              StatusCodes.Status404NotFound,
              "PRODUCT_NOT_FOUND",
              "Produk tidak ditemukan.");
        }

        return Ok(new { data = gallery });
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Galeri gagal dimuat untuk produk {ProductId}.",
            productId);

        return ErrorResponse(
            StatusCodes.Status500InternalServerError,
            "PRODUCT_GALLERY_LOAD_FAILED",
            "Galeri belum dapat dimuat. Silakan coba kembali.");
      }
    }

    [HttpPut("order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Reorder(
    Guid productId,
    [FromBody] ReorderProductImagesRequest request,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      try
      {
        var status = await _imageService.ReorderAsync(
            userId,
            productId,
            request.ImageIds,
            cancellationToken);

        return status switch
        {
          ProductImageReorderStatus.Updated => Ok(new
          {
            code = "PRODUCT_IMAGES_REORDERED",
            message = "Urutan gambar berhasil diperbarui."
          }),

          ProductImageReorderStatus.ProductNotFound => NotFound(new
          {
            code = "PRODUCT_NOT_FOUND",
            message = "Produk tidak ditemukan."
          }),

          ProductImageReorderStatus.InvalidOrder => BadRequest(new
          {
            code = "PRODUCT_IMAGE_ORDER_INVALID",
            message =
                  "Kirim seluruh ID gambar produk tepat satu kali sesuai urutan yang diinginkan. Jika galeri berubah, muat ulang sebelum mencoba kembali."
          }),

          _ => throw new InvalidOperationException(
              "Status pengurutan gambar tidak dikenali.")
        };
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Gagal mengubah urutan gambar produk {ProductId}.",
            productId);

        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new
            {
              code = "PRODUCT_IMAGE_REORDER_FAILED",
              message = "Urutan gambar belum dapat diperbarui."
            });
      }
    }

    private IActionResult ProcessingError(
        ProductImageProcessingStatus? status)
    {
      return status switch
      {
        ProductImageProcessingStatus.EmptyFile =>
            ErrorResponse(
                StatusCodes.Status400BadRequest,
                "PRODUCT_IMAGE_EMPTY",
                "File gambar tidak boleh kosong."),

        ProductImageProcessingStatus.FileTooLarge =>
            ErrorResponse(
                StatusCodes.Status413PayloadTooLarge,
                "PRODUCT_IMAGE_TOO_LARGE",
                "Ukuran gambar maksimal 5 MiB."),

        ProductImageProcessingStatus.DimensionsTooLarge =>
            ErrorResponse(
                StatusCodes.Status400BadRequest,
                "PRODUCT_IMAGE_DIMENSIONS_TOO_LARGE",
                "Dimensi gambar maksimal 6.000 piksel per sisi "
                + "dan total 16 megapiksel."),

        ProductImageProcessingStatus
            .AnimatedImageNotSupported =>
            ErrorResponse(
                StatusCodes.Status400BadRequest,
                "PRODUCT_IMAGE_ANIMATION_NOT_SUPPORTED",
                "Gunakan gambar tanpa animasi."),

        _ => ErrorResponse(
            StatusCodes.Status400BadRequest,
            "PRODUCT_IMAGE_INVALID",
            "File harus berupa gambar JPEG, PNG, atau WebP "
            + "yang dapat dibaca.")
      };
    }

    [HttpPut("option-values/{optionValueId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetOptionImage(
    Guid productId,
    Guid optionValueId,
    [FromBody] SetProductOptionImageRequest request,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      try
      {
        var status = await _imageService.SetOptionImageAsync(
            userId,
            productId,
            optionValueId,
            request.ImageId,
            cancellationToken);

        return status switch
        {
          ProductOptionImageStatus.Updated => Ok(new
          {
            code = "PRODUCT_OPTION_IMAGE_UPDATED",
            message = request.ImageId.HasValue
                  ? "Gambar berhasil dihubungkan ke nilai opsi."
                  : "Hubungan gambar pada nilai opsi berhasil dilepas.",
            data = new
            {
              optionValueId,
              imageId = request.ImageId
            }
          }),

          ProductOptionImageStatus.ProductNotFound => NotFound(new
          {
            code = "PRODUCT_NOT_FOUND",
            message = "Produk tidak ditemukan."
          }),

          ProductOptionImageStatus.OptionValueNotFound => NotFound(new
          {
            code = "PRODUCT_OPTION_VALUE_NOT_FOUND",
            message = "Nilai opsi tidak ditemukan pada produk ini."
          }),

          ProductOptionImageStatus.ImageNotFound => NotFound(new
          {
            code = "PRODUCT_IMAGE_NOT_FOUND",
            message = "Gambar tidak ditemukan pada produk ini."
          }),

          ProductOptionImageStatus.InvalidImageId => BadRequest(new
          {
            code = "PRODUCT_IMAGE_ID_INVALID",
            message = "ID gambar tidak boleh berupa GUID kosong."
          }),

          _ => throw new InvalidOperationException(
              "Status pengaturan gambar opsi tidak dikenali.")
        };
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Gagal mengatur gambar nilai opsi {OptionValueId} pada produk {ProductId}.",
            optionValueId,
            productId);

        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new
            {
              code = "PRODUCT_OPTION_IMAGE_UPDATE_FAILED",
              message = "Gambar nilai opsi belum dapat diperbarui."
            });
      }
    }

    [HttpDelete("{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
    Guid productId,
    Guid imageId,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      try
      {
        var status = await _imageService.DeleteAsync(
            userId,
            productId,
            imageId,
            cancellationToken);

        return status switch
        {
          ProductImageDeleteStatus.Deleted => Ok(new
          {
            code = "PRODUCT_IMAGE_DELETED",
            message = "Gambar dihapus dari galeri. Penghapusan file dijadwalkan.",
            data = new
            {
              imageId,
              storageDeletionQueued = true
            }
          }),

          ProductImageDeleteStatus.ProductNotFound => NotFound(new
          {
            code = "PRODUCT_NOT_FOUND",
            message = "Produk tidak ditemukan."
          }),

          ProductImageDeleteStatus.ImageNotFound => NotFound(new
          {
            code = "PRODUCT_IMAGE_NOT_FOUND",
            message = "Gambar tidak ditemukan pada produk ini."
          }),

          ProductImageDeleteStatus.ImageInUse => Conflict(new
          {
            code = "PRODUCT_IMAGE_IN_USE",
            message =
                  "Gambar masih digunakan oleh nilai opsi. Lepaskan atau ganti hubungan gambar terlebih dahulu."
          }),

          ProductImageDeleteStatus.LastImageOfActiveProduct => Conflict(new
          {
            code = "PRODUCT_LAST_IMAGE_REQUIRED",
            message =
        "Foto terakhir produk aktif tidak dapat dihapus. Tambahkan foto pengganti atau nonaktifkan produk terlebih dahulu."
          }),

          _ => throw new InvalidOperationException(
              "Status penghapusan gambar tidak dikenali.")
        };
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Gagal menghapus gambar {ImageId} pada produk {ProductId}.",
            imageId,
            productId);

        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new
            {
              code = "PRODUCT_IMAGE_DELETE_FAILED",
              message = "Gambar belum dapat dihapus."
            });
      }
    }

    private IActionResult ErrorResponse(
        int statusCode,
        string code,
        string message)
    {
      return StatusCode(statusCode, new { code, message });
    }

    private bool TryGetUserId(out Guid userId)
    {
      var value =
          User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? User.FindFirstValue("sub");

      return Guid.TryParse(value, out userId)
          && userId != Guid.Empty;
    }
  }
}