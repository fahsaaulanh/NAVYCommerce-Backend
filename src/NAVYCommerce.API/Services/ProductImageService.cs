using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.Application.Abstractions.Imaging;
using NAVYCommerce.Application.Abstractions.Storage;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.Infrastructure.Data.Entities;

namespace NAVYCommerce.API.Services
{
  public sealed class ProductImageService : IProductImageService
  {
    private const int MaxImagesPerProduct = 10;
    private const int MaxAltTextLength = 250;

    private readonly ApplicationDbContext _context;
    private readonly IProductImageProcessor _processor;
    private readonly IProductImageStorage _storage;
    private readonly ILogger<ProductImageService> _logger;
    private const int MaxImages = 10;

    public ProductImageService(
        ApplicationDbContext context,
        IProductImageProcessor processor,
        IProductImageStorage storage,
        ILogger<ProductImageService> logger)
    {
      _context = context;
      _processor = processor;
      _storage = storage;
      _logger = logger;
    }

    public async Task<ProductImageUploadResult> UploadAsync(
        Guid ownerId,
        Guid productId,
        Stream content,
        string? altText,
        CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty || productId == Guid.Empty)
      {
        return new(
            ProductImageUploadStatus.ProductNotFound);
      }

      var normalizedAltText = altText?.Trim();

      if (normalizedAltText?.Length > MaxAltTextLength)
      {
        return new(
            ProductImageUploadStatus.InvalidAltText);
      }

      if (!await CanManageProductAsync(
          ownerId,
          productId,
          cancellationToken))
      {
        return new(
            ProductImageUploadStatus.ProductNotFound);
      }

      var initialImageCount = await _context.ProductImages
          .CountAsync(
              image => image.ProductId == productId,
              cancellationToken);

      if (initialImageCount >= MaxImagesPerProduct)
      {
        return new(
            ProductImageUploadStatus.ImageLimitReached);
      }

      var processingResult = await _processor.ProcessAsync(
          content,
          cancellationToken);

      if (processingResult.Status
          != ProductImageProcessingStatus.Success)
      {
        return new(
            ProductImageUploadStatus.InvalidImage,
            ProcessingStatus: processingResult.Status);
      }

      var processedImage = processingResult.Image
          ?? throw new InvalidOperationException(
              "Hasil pemrosesan gambar tidak tersedia.");

      string? storageKey = null;
      var commitAttempted = false;
      var committed = false;

      try
      {
        using var uploadContent = new MemoryStream(
            processedImage.Content,
            writable: false);

        storageKey = await _storage.SaveAsync(
            productId,
            uploadContent,
            processedImage.ContentType,
            cancellationToken);

        var readUrl = await _storage.CreateReadUrlAsync(
            storageKey,
            cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        var lockedProducts = await _context.Products
            .FromSqlInterpolated(
                $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var product = lockedProducts.SingleOrDefault();

        if (product == null
            || !await CanManageProductAsync(
                ownerId,
                productId,
                cancellationToken))
        {
          return new(
              ProductImageUploadStatus.ProductNotFound);
        }

        var imagesQuery = _context.ProductImages
            .Where(image => image.ProductId == productId);

        var imageCount = await imagesQuery.CountAsync(
            cancellationToken);

        if (imageCount >= MaxImagesPerProduct)
        {
          return new(
              ProductImageUploadStatus.ImageLimitReached);
        }

        var lastSortOrder = await imagesQuery
            .Select(image => (int?)image.SortOrder)
            .MaxAsync(cancellationToken);

        var image = new ProductImage
        {
          Id = Guid.NewGuid(),
          ProductId = productId,
          StorageKey = storageKey,
          AltText = string.IsNullOrWhiteSpace(
                normalizedAltText)
                ? product.Title
                : normalizedAltText,
          SortOrder = (lastSortOrder ?? -1) + 1,
          CreatedAt = DateTime.UtcNow
        };

        _context.ProductImages.Add(image);

        await _context.SaveChangesAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        commitAttempted = true;

        await transaction.CommitAsync(
            CancellationToken.None);

        committed = true;

        return new(
            ProductImageUploadStatus.Uploaded,
            new ProductImageResponse(
                image.Id,
                readUrl.Url,
                readUrl.ExpiresAt,
                image.AltText,
                image.SortOrder,
                image.CreatedAt));
      }
      finally
      {
        if (storageKey != null && !committed)
        {
          if (!commitAttempted)
          {
            await TryDeleteUploadedFileAsync(storageKey);
          }
          else
          {
            _logger.LogError(
                "Hasil commit metadata gambar belum dapat "
                + "dipastikan. Periksa storage key {StorageKey}.",
                storageKey);
          }
        }
      }
    }

    public async Task<IReadOnlyList<ProductImageResponse>?>
    GetOwnedGalleryAsync(
        Guid ownerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty
          || productId == Guid.Empty
          || !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return null;
      }

      var images = await _context.ProductImages
          .AsNoTracking()
          .Where(image =>
              image.ProductId == productId
              && image.Product.Store.OwnerId == ownerId
              && image.Product.Store.Owner.IsVerified
              && image.Product.Store.Owner.Role == UserRole.Seller)
          .OrderBy(image => image.SortOrder)
          .ThenBy(image => image.CreatedAt)
          .ThenBy(image => image.Id)
          .Select(image => new
          {
            image.Id,
            image.StorageKey,
            image.AltText,
            image.SortOrder,
            image.CreatedAt
          })
          .ToListAsync(cancellationToken);

      var gallery = new List<ProductImageResponse>(images.Count);

      foreach (var image in images)
      {
        var readUrl = await _storage.CreateReadUrlAsync(
            image.StorageKey,
            cancellationToken);

        gallery.Add(new ProductImageResponse(
            image.Id,
            readUrl.Url,
            readUrl.ExpiresAt,
            image.AltText,
            image.SortOrder,
            image.CreatedAt));
      }

      return gallery;
    }

    public async Task<ProductImageReorderStatus> ReorderAsync(
    Guid ownerId,
    Guid productId,
    IReadOnlyList<Guid> imageIds,
    CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty ||
          productId == Guid.Empty ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageReorderStatus.ProductNotFound;
      }

      if (imageIds == null ||
          imageIds.Count > MaxImages ||
          imageIds.Any(id => id == Guid.Empty) ||
          imageIds.Distinct().Count() != imageIds.Count)
      {
        return ProductImageReorderStatus.InvalidOrder;
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var lockedProducts = await _context.Products
          .FromSqlInterpolated(
              $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
          .AsNoTracking()
          .ToListAsync(cancellationToken);

      if (lockedProducts.Count == 0 ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageReorderStatus.ProductNotFound;
      }

      var images = await _context.ProductImages
          .Where(image => image.ProductId == productId)
          .ToListAsync(cancellationToken);

      var existingIds = images
          .Select(image => image.Id)
          .ToHashSet();

      if (imageIds.Count != images.Count ||
          !existingIds.SetEquals(imageIds))
      {
        return ProductImageReorderStatus.InvalidOrder;
      }

      var imagesById = images.ToDictionary(image => image.Id);

      for (var index = 0; index < imageIds.Count; index++)
      {
        var imageId = imageIds[index];

        imagesById[imageId].SortOrder = index;
      }

      await _context.SaveChangesAsync(cancellationToken);

      await transaction.CommitAsync(cancellationToken);

      return ProductImageReorderStatus.Updated;
    }

    public async Task<ProductOptionImageStatus> SetOptionImageAsync(
    Guid ownerId,
    Guid productId,
    Guid optionValueId,
    Guid? imageId,
    CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty ||
          productId == Guid.Empty ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductOptionImageStatus.ProductNotFound;
      }

      if (imageId == Guid.Empty)
      {
        return ProductOptionImageStatus.InvalidImageId;
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);


      var lockedProducts = await _context.Products
          .FromSqlInterpolated(
              $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
          .AsNoTracking()
          .ToListAsync(cancellationToken);

      if (lockedProducts.Count == 0 ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductOptionImageStatus.ProductNotFound;
      }

      var optionValue = await _context.ProductOptionValues
          .SingleOrDefaultAsync(
              value =>
                  value.ProductId == productId &&
                  value.Id == optionValueId,
              cancellationToken);

      if (optionValue == null)
      {
        return ProductOptionImageStatus.OptionValueNotFound;
      }

      if (imageId.HasValue)
      {
        var imageExists = await _context.ProductImages
            .AnyAsync(
                image =>
                    image.ProductId == productId &&
                    image.Id == imageId.Value,
                cancellationToken);

        if (!imageExists)
        {
          return ProductOptionImageStatus.ImageNotFound;
        }
      }

      optionValue.ImageId = imageId;

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return ProductOptionImageStatus.Updated;
    }

    public async Task<ProductImageAltTextStatus> UpdateAltTextAsync(
    Guid ownerId,
    Guid productId,
    Guid imageId,
    string? altText,
    CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty ||
          productId == Guid.Empty ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageAltTextStatus.ProductNotFound;
      }

      var normalizedAltText = altText?.Trim();

      if (string.IsNullOrWhiteSpace(normalizedAltText) ||
          normalizedAltText.Length > 250)
      {
        return ProductImageAltTextStatus.InvalidAltText;
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var rows = await _context.Products
          .FromSqlInterpolated(
              $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
          .AsNoTracking()
          .ToListAsync(cancellationToken);

      var lockedProduct = rows.SingleOrDefault();

      if (lockedProduct == null ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageAltTextStatus.ProductNotFound;
      }

      var image = await _context.ProductImages
          .SingleOrDefaultAsync(
              image =>
                  image.ProductId == productId &&
                  image.Id == imageId,
              cancellationToken);

      if (image == null)
      {
        return ProductImageAltTextStatus.ImageNotFound;
      }

      image.AltText = normalizedAltText;

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return ProductImageAltTextStatus.Updated;
    }

    public async Task<ProductImageDeleteStatus> DeleteAsync(
    Guid ownerId,
    Guid productId,
    Guid imageId,
    CancellationToken cancellationToken = default)
    {
      if (ownerId == Guid.Empty ||
          productId == Guid.Empty ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageDeleteStatus.ProductNotFound;
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var rows = await _context.Products
          .FromSqlInterpolated(
              $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
          .AsNoTracking()
          .ToListAsync(cancellationToken);

      var lockedProduct = rows.SingleOrDefault();

      if (lockedProduct == null ||
          !await CanManageProductAsync(
              ownerId,
              productId,
              cancellationToken))
      {
        return ProductImageDeleteStatus.ProductNotFound;
      }

      var image = await _context.ProductImages
          .SingleOrDefaultAsync(
              image =>
                  image.ProductId == productId &&
                  image.Id == imageId,
              cancellationToken);

      if (image == null)
      {
        return ProductImageDeleteStatus.ImageNotFound;
      }

      var imageInUse = await _context.ProductOptionValues
          .AnyAsync(
              value =>
                  value.ProductId == productId &&
                  value.ImageId == imageId,
              cancellationToken);

      if (imageInUse)
      {
        return ProductImageDeleteStatus.ImageInUse;
      }

      var now = DateTime.UtcNow;

      _context.StorageDeletionJobs.Add(new StorageDeletionJob
      {
        StorageKey = image.StorageKey,
        CreatedAt = now,
        NextAttemptAt = now
      });

      _context.ProductImages.Remove(image);

      var remainingImages = await _context.ProductImages
          .Where(item =>
              item.ProductId == productId &&
              item.Id != imageId)
          .OrderBy(item => item.SortOrder)
          .ThenBy(item => item.CreatedAt)
          .ThenBy(item => item.Id)
          .ToListAsync(cancellationToken);

      for (var index = 0; index < remainingImages.Count; index++)
      {
        remainingImages[index].SortOrder = index;
      }

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return ProductImageDeleteStatus.Deleted;
    }

    private Task<bool> CanManageProductAsync(
        Guid ownerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
      return _context.Products.AnyAsync(
          product =>
              product.Id == productId
              && product.Store.OwnerId == ownerId
              && product.Store.Owner.IsVerified
              && product.Store.Owner.Role == UserRole.Seller,
          cancellationToken);
    }


    private async Task TryDeleteUploadedFileAsync(
        string storageKey)
    {
      using var cleanupCancellation =
          new CancellationTokenSource(
              TimeSpan.FromSeconds(10));

      try
      {
        await _storage.DeleteAsync(
            storageKey,
            cleanupCancellation.Token);
      }
      catch (Exception exception)
      {
        _logger.LogWarning(
            exception,
            "Cleanup gambar gagal untuk storage key {StorageKey}.",
            storageKey);
      }
    }
  }
}