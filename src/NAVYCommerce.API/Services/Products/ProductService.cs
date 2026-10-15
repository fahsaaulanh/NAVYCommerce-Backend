using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.Application.Abstractions.Storage;
using System.Collections.Generic;

namespace NAVYCommerce.API.Services
{
  public class ProductService : IProductService
  {
    private readonly ApplicationDbContext _context;
    private readonly IProductVariantFactory _variantFactory;
    private readonly IProductImageStorage _imageStorage;

    public ProductService(
        ApplicationDbContext context,
        IProductVariantFactory variantFactory,
        IProductImageStorage imageStorage)
    {
      _context = context;
      _variantFactory = variantFactory;
      _imageStorage = imageStorage;
    }

    public async Task<CreateProductResult> CreateAsync(
        Guid ownerId,
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
      var storeId = request.StoreId!.Value;
      var categoryId = request.CategoryId!.Value;

      var ownsStore = await _context.Stores
          .AnyAsync(
              store =>
                  store.Id == storeId &&
                  store.OwnerId == ownerId &&
                  store.Owner.Role == UserRole.Seller &&
                  store.Owner.IsVerified,
              cancellationToken);

      if (!ownsStore)
      {
        return new(CreateProductStatus.StoreNotFound);
      }

      var categoryExists = await _context.Categories
          .AnyAsync(
              category => category.Id == categoryId,
              cancellationToken);

      if (!categoryExists)
      {
        return new(CreateProductStatus.CategoryNotFound);
      }

      var slugExists = await _context.Products
          .AnyAsync(
              product => product.Slug == request.Slug,
              cancellationToken);

      if (slugExists)
      {
        return new(CreateProductStatus.SlugAlreadyUsed);
      }

      var product = new Product
      {
        Id = Guid.NewGuid(),
        StoreId = storeId,
        CategoryId = categoryId,
        Title = request.Title.Trim(),
        Slug = request.Slug,
        Description = string.IsNullOrWhiteSpace(request.Description)
              ? null
              : request.Description.Trim(),
        WeightGrams = request.WeightGrams!.Value,
        BasePrice = request.BasePrice!.Value,

        IsActive = false,
        CreatedAt = DateTime.UtcNow
      };

      for (var optionIndex = 0;
           optionIndex < request.Options!.Count;
           optionIndex++)
      {
        var optionRequest = request.Options[optionIndex]!;

        var option = new ProductOption
        {
          Id = Guid.NewGuid(),
          ProductId = product.Id,
          Product = product,
          Name = optionRequest.Name.Trim(),
          SortOrder = optionIndex
        };

        for (var valueIndex = 0;
             valueIndex < optionRequest.Values!.Count;
             valueIndex++)
        {
          option.Values.Add(new ProductOptionValue
          {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            OptionId = option.Id,
            Option = option,
            Value = optionRequest.Values[valueIndex]!.Trim(),
            SortOrder = valueIndex,
            ImageId = null
          });
        }

        product.Options.Add(option);
      }

      var variants = _variantFactory.CreateVariants(product);

      foreach (var variant in variants)
      {
        product.Variants.Add(variant);
      }

      _context.Products.Add(product);

      try
      {
        await _context.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException exception)
          when (exception.InnerException is PostgresException
          {
            SqlState: "23505",
            ConstraintName: "IX_products_Slug"
          })
      {
        return new(CreateProductStatus.SlugAlreadyUsed);
      }

      return new(
          CreateProductStatus.Created,
          ToResponse(product));
    }

    public async Task<ProductDetailResponse?> GetOwnedProductAsync(
        Guid ownerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
      var product = await _context.Products
          .AsNoTracking()
          .Where(product =>
              product.Id == productId &&
              product.Store.OwnerId == ownerId &&
              product.Store.Owner.Role == UserRole.Seller &&
              product.Store.Owner.IsVerified)
          .Include(product => product.Options)
              .ThenInclude(option => option.Values)
          .Include(product => product.Variants)
              .ThenInclude(variant => variant.OptionValues)
          .AsSplitQuery()
          .SingleOrDefaultAsync(cancellationToken);

      return product == null ? null : ToResponse(product);
    }

    public async Task<SellerProductListResponse?> GetOwnedByStoreAsync(
    Guid ownerId,
    Guid storeId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
      if (page < 1 || page > 1_000_000)
      {
        throw new ArgumentOutOfRangeException(nameof(page));
      }

      if (pageSize < 1 || pageSize > 100)
      {
        throw new ArgumentOutOfRangeException(nameof(pageSize));
      }

      if (ownerId == Guid.Empty || storeId == Guid.Empty)
      {
        return null;
      }

      var canAccessStore = await _context.Stores
          .AnyAsync(
              store =>
                  store.Id == storeId &&
                  store.OwnerId == ownerId &&
                  store.Owner.IsVerified &&
                  store.Owner.Role == UserRole.Seller,
              cancellationToken);

      if (!canAccessStore)
      {
        return null;
      }

      var query = _context.Products
          .AsNoTracking()
          .Where(product =>
              product.StoreId == storeId &&
              product.Store.OwnerId == ownerId &&
              product.Store.Owner.IsVerified &&
              product.Store.Owner.Role == UserRole.Seller);

      var total = await query.CountAsync(cancellationToken);

      var products = await query
          .OrderByDescending(product => product.CreatedAt)
          .ThenBy(product => product.Id)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(product => new
          {
            product.Id,
            product.StoreId,
            product.CategoryId,

            CategoryName = product.Category != null
                  ? product.Category.Name
                  : null,

            product.Title,
            product.Slug,
            product.BasePrice,
            product.IsActive,
            product.CreatedAt,

            VariantCount = product.Variants.Count,

            TotalStock = product.Variants
                  .Select(variant => (long?)variant.StockQuantity)
                  .Sum() ?? 0L,

            ImageCount = product.Images.Count,

            Thumbnail = product.Images
                  .OrderBy(image => image.SortOrder)
                  .ThenBy(image => image.CreatedAt)
                  .ThenBy(image => image.Id)
                  .Select(image => new
                  {
                    image.StorageKey,
                    image.AltText
                  })
                  .FirstOrDefault()
          })
          .ToListAsync(cancellationToken);

      var items = new List<SellerProductSummaryResponse>(
          products.Count);

      foreach (var product in products)
      {
        SellerProductThumbnailResponse? thumbnail = null;

        if (product.Thumbnail != null)
        {
          var readUrl = await _imageStorage.CreateReadUrlAsync(
              product.Thumbnail.StorageKey,
              cancellationToken);

          thumbnail = new SellerProductThumbnailResponse(
              readUrl.Url,
              readUrl.ExpiresAt,
              product.Thumbnail.AltText);
        }

        items.Add(new SellerProductSummaryResponse(
            product.Id,
            product.StoreId,
            product.CategoryId,
            product.CategoryName,
            product.Title,
            product.Slug,
            product.BasePrice,
            product.IsActive,
            product.CreatedAt,
            product.VariantCount,
            product.TotalStock,
            product.ImageCount,
            thumbnail));
      }

      var totalPages = (int)Math.Ceiling(
          total / (double)pageSize);

      return new SellerProductListResponse(
          items,
          new SellerProductPaginationResponse(
              page,
              pageSize,
              total,
              totalPages));
    }

    public async Task<ProductListResponse> GetPublicProductsAsync(
        ProductCatalogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(request);
      // Also guard service calls that do not pass through MVC validation.
      Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

      var query = PublicProductsQuery();
      var search = request.Search?.Trim();

      if (!string.IsNullOrEmpty(search))
      {
        var pattern = "%" + EscapeLikePattern(search) + "%";
        query = query.Where(product =>
            EF.Functions.ILike(product.Title, pattern, "\\") ||
            EF.Functions.ILike(product.Store.Name, pattern, "\\"));
      }

      if (request.CategoryId.HasValue)
      {
        query = query.Where(product => product.CategoryId == request.CategoryId.Value);
      }

      if (request.InStock)
      {
        query = query.Where(product => product.Variants.Any(variant => variant.StockQuantity > 0));
      }

      var summaries = query
          .Select(product => new
          {
            product.Id,
            product.CreatedAt,
            product.StoreId,
            product.CategoryId,
            product.Title,
            product.Slug,
            product.BasePrice,

            StoreName = product.Store.Name,
            StoreSlug = product.Store.Slug,

            CategoryName = product.Category != null
                  ? product.Category.Name
                  : null,

            MinPrice = product.Variants
                  .Select(variant =>
                      (decimal?)(product.BasePrice +
                          variant.PriceAdjustment))
                  .Min() ?? product.BasePrice,

            MaxPrice = product.Variants
                  .Select(variant =>
                      (decimal?)(product.BasePrice +
                          variant.PriceAdjustment))
                  .Max() ?? product.BasePrice,

            HasStock = product.Variants
                  .Any(variant => variant.StockQuantity > 0),

            Thumbnail = product.Images
                  .OrderBy(image => image.SortOrder)
                  .ThenBy(image => image.CreatedAt)
                  .ThenBy(image => image.Id)
                  .Select(image => new
                  {
                    image.StorageKey,
                    image.AltText
                  })
                  .FirstOrDefault()
          });

      // Filter the same lowest variant price shown on the catalog card.
      if (request.MinPrice.HasValue)
      {
        summaries = summaries.Where(item => item.MinPrice >= request.MinPrice.Value);
      }
      if (request.MaxPrice.HasValue)
      {
        summaries = summaries.Where(item => item.MinPrice <= request.MaxPrice.Value);
      }

      var total = await summaries.CountAsync(cancellationToken);

      var ordered = request.Sort switch
      {
        "price-asc" => summaries.OrderBy(item => item.MinPrice).ThenBy(item => item.Id),
        "price-desc" => summaries.OrderByDescending(item => item.MinPrice).ThenBy(item => item.Id),
        "name" => summaries.OrderBy(item => item.Title).ThenBy(item => item.Id),
        _ => summaries.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
      };

      var products = await ordered
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .ToListAsync(cancellationToken);

      var items = new List<ProductSummaryResponse>(products.Count);

      foreach (var product in products)
      {
        PublicProductThumbnailResponse? thumbnail = null;

        if (product.Thumbnail != null)
        {
          var readUrl = await _imageStorage.CreateReadUrlAsync(
              product.Thumbnail.StorageKey,
              cancellationToken);

          thumbnail = new PublicProductThumbnailResponse(
              readUrl.Url,
              readUrl.ExpiresAt,
              product.Thumbnail.AltText);
        }

        items.Add(new ProductSummaryResponse(
            product.Id,
            product.StoreId,
            product.CategoryId,
            product.Title,
            product.Slug,
            product.BasePrice,
            product.StoreName,
            product.StoreSlug,
            product.CategoryName,
            product.MinPrice,
            product.MaxPrice,
            product.HasStock,
            thumbnail));
      }

      return new ProductListResponse(
          items,
          request.Page,
          request.PageSize,
          total);
    }

    private IQueryable<Product> PublicProductsQuery()
    {
      return _context.Products.AsNoTracking().Where(product =>
          product.IsActive &&
          product.Store.Owner.IsVerified &&
          product.Store.Owner.Role == UserRole.Seller);
    }

    private static string EscapeLikePattern(string value)
    {
      return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    public async Task<IReadOnlyList<CatalogCategoryResponse>> GetCatalogCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
      return await PublicProductsQuery()
          .GroupBy(product => new
          {
            Id = product.CategoryId,
            product.Category!.ParentId,
            product.Category.Name,
            product.Category.Slug
          })
          .Select(group => new CatalogCategoryResponse(
              group.Key.Id,
              group.Key.ParentId,
              group.Key.Name,
              group.Key.Slug,
              group.Count()))
          .OrderBy(category => category.Name)
          .ThenBy(category => category.Id)
          .ToListAsync(cancellationToken);
    }

    public async Task<PublicProductDetailResponse?>
    GetPublicProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
      if (productId == Guid.Empty)
      {
        return null;
      }

      var product = await PublicProductsQuery()
          .Where(item => item.Id == productId)
          .Include(item => item.Store)
          .Include(item => item.Category)
          .Include(item => item.Images)
          .Include(item => item.Options)
              .ThenInclude(option => option.Values)
          .Include(item => item.Variants)
              .ThenInclude(variant => variant.OptionValues)
          .AsSplitQuery()
          .SingleOrDefaultAsync(cancellationToken);

      if (product == null)
      {
        return null;
      }

      var images = new List<PublicProductGalleryImageResponse>(
          product.Images.Count);

      foreach (var image in product.Images
          .OrderBy(item => item.SortOrder)
          .ThenBy(item => item.CreatedAt)
          .ThenBy(item => item.Id))
      {
        var readUrl = await _imageStorage.CreateReadUrlAsync(
            image.StorageKey,
            cancellationToken);

        images.Add(new PublicProductGalleryImageResponse(
            image.Id,
            readUrl.Url,
            readUrl.ExpiresAt,
            image.AltText,
            image.SortOrder,
            image.CreatedAt));
      }

      return new PublicProductDetailResponse(
          ToResponse(product),
          new PublicProductStoreResponse(
              product.Store.Id,
              product.Store.Name,
              product.Store.Slug),
          product.Category?.Name,
          images);
    }

    public async Task<SetProductPublicationResult> SetPublicationAsync(
    Guid ownerId,
    Guid productId,
    bool isActive,
    CancellationToken cancellationToken = default)
    {
      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var product = await FindAndLockOwnedProductAsync(
          ownerId,
          productId,
          cancellationToken);

      if (product == null)
      {
        return new(SetProductPublicationStatus.NotFound);
      }

      if (product.IsActive == isActive)
      {
        await transaction.CommitAsync(cancellationToken);

        return new(
            SetProductPublicationStatus.Updated,
            new ProductPublicationResponse(
                product.Id,
                product.IsActive));
      }

      if (isActive)
      {
        var issues = new List<ProductPublicationIssue>();

        var hasImage = await _context.ProductImages
            .AnyAsync(
                image => image.ProductId == productId,
                cancellationToken);

        if (!hasImage)
        {
          issues.Add(new(
              "PRODUCT_IMAGE_REQUIRED",
              "Unggah minimal satu foto produk."));
        }

        var variants = await _context.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == productId)
            .Select(variant => new
            {
              variant.PriceAdjustment,
              variant.StockQuantity
            })
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
          issues.Add(new(
              "PRODUCT_VARIANT_REQUIRED",
              "Produk belum memiliki varian."));
        }
        else
        {
          if (!variants.Any(variant => variant.StockQuantity > 0))
          {
            issues.Add(new(
                "PRODUCT_STOCK_REQUIRED",
                "Isi stok minimal satu varian sebelum publikasi."));
          }

          if (variants.Any(variant => variant.StockQuantity < 0))
          {
            issues.Add(new(
                "PRODUCT_STOCK_INVALID",
                "Stok varian tidak boleh negatif."));
          }
        }

        const decimal maxPrice = 999_999_999_999.99m;

        var validBasePrice =
            product.BasePrice >= 0 &&
            product.BasePrice <= maxPrice;

        var hasInvalidPrice =
            !validBasePrice ||
            variants.Any(variant =>
                variant.PriceAdjustment <= -product.BasePrice ||
                variant.PriceAdjustment >
                    maxPrice - product.BasePrice);

        if (hasInvalidPrice)
        {
          issues.Add(new(
              "PRODUCT_PRICE_INVALID",
              "Harga akhir setiap varian harus lebih dari 0 dan maksimal 999.999.999.999,99."));
        }

        if (issues.Count > 0)
        {
          return new(
              SetProductPublicationStatus.NotReady,
              Issues: issues);
        }
      }

      product.IsActive = isActive;

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return new(
          SetProductPublicationStatus.Updated,
          new ProductPublicationResponse(
              product.Id,
              product.IsActive));
    }

    public async Task<UpdateProductVariantResult> UpdateVariantAsync(
      Guid ownerId,
      Guid productId,
      Guid variantId,
      UpdateProductVariantRequest request,
      CancellationToken cancellationToken = default)
    {
      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var product = await FindAndLockOwnedProductAsync(
          ownerId,
          productId,
          cancellationToken);

      if (product == null)
      {
        return new(UpdateProductVariantStatus.NotFound);
      }

      var variant = await _context.ProductVariants
          .Include(item => item.OptionValues)
          .SingleOrDefaultAsync(
              item =>
                  item.Id == variantId &&
                  item.ProductId == productId,
              cancellationToken);

      if (variant == null)
      {
        return new(UpdateProductVariantStatus.NotFound);
      }

      var basePrice = product.BasePrice;
      var adjustment = request.PriceAdjustment!.Value;

      const decimal maxPrice = 999_999_999_999.99m;

      if (basePrice < 0 ||
          basePrice > maxPrice ||
          adjustment < -basePrice ||
          adjustment > maxPrice - basePrice)
      {
        return new(UpdateProductVariantStatus.InvalidPrice);
      }

      var finalPrice = basePrice + adjustment;

      if (product.IsActive && finalPrice <= 0)
      {
        return new(UpdateProductVariantStatus.InvalidPrice);
      }

      variant.PriceAdjustment = adjustment;
      variant.StockQuantity = request.StockQuantity!.Value;

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      var response = new ProductVariantResponse(
          variant.Id,
          variant.Sku,
          variant.OptionValues
              .OrderBy(link => link.OptionId)
              .Select(link => link.OptionValueId)
              .ToList(),
          variant.PriceAdjustment,
          finalPrice,
          variant.StockQuantity);

      return new(
          UpdateProductVariantStatus.Updated,
          response);
    }

    public async Task<RenameProductLabelResult> RenameOptionAsync(
      Guid ownerId,
      Guid productId,
      Guid optionId,
      string name,
      CancellationToken cancellationToken = default)
    {
      var label = name?.Trim();

      if (string.IsNullOrWhiteSpace(label) || label.Length > 50)
      {
        return new(RenameProductLabelStatus.InvalidLabel);
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var product = await FindAndLockOwnedProductAsync(
          ownerId,
          productId,
          cancellationToken);

      if (product == null)
      {
        return new(RenameProductLabelStatus.NotFound);
      }

      var option = await _context.ProductOptions
          .SingleOrDefaultAsync(
              item =>
                  item.ProductId == productId &&
                  item.Id == optionId,
              cancellationToken);

      if (option == null)
      {
        return new(RenameProductLabelStatus.NotFound);
      }

      var normalizedLabel = label.ToLowerInvariant();

      var duplicate = await _context.ProductOptions
          .AnyAsync(
              item =>
                  item.ProductId == productId &&
                  item.Id != optionId &&
                  item.Name.ToLower() == normalizedLabel,
              cancellationToken);

      if (duplicate)
      {
        return new(RenameProductLabelStatus.Duplicate);
      }

      option.Name = label;

      try
      {
        await _context.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException exception)
          when (exception.InnerException is PostgresException
          {
            SqlState: "23505",
            ConstraintName: "IX_product_options_ProductId_Name"
          })
      {
        return new(RenameProductLabelStatus.Duplicate);
      }

      await transaction.CommitAsync(cancellationToken);

      return new(
          RenameProductLabelStatus.Updated,
          option.Name);
    }

    public async Task<RenameProductLabelResult> RenameOptionValueAsync(
    Guid ownerId,
    Guid productId,
    Guid optionValueId,
    string value,
    CancellationToken cancellationToken = default)
    {
      var label = value?.Trim();

      if (string.IsNullOrWhiteSpace(label) || label.Length > 50)
      {
        return new(RenameProductLabelStatus.InvalidLabel);
      }

      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var product = await FindAndLockOwnedProductAsync(
          ownerId,
          productId,
          cancellationToken);

      if (product == null)
      {
        return new(RenameProductLabelStatus.NotFound);
      }

      var optionValue = await _context.ProductOptionValues
          .SingleOrDefaultAsync(
              item =>
                  item.ProductId == productId &&
                  item.Id == optionValueId,
              cancellationToken);

      if (optionValue == null)
      {
        return new(RenameProductLabelStatus.NotFound);
      }

      var normalizedLabel = label.ToLowerInvariant();

      var duplicate = await _context.ProductOptionValues
          .AnyAsync(
              item =>
                  item.ProductId == productId &&
                  item.OptionId == optionValue.OptionId &&
                  item.Id != optionValueId &&
                  item.Value.ToLower() == normalizedLabel,
              cancellationToken);

      if (duplicate)
      {
        return new(RenameProductLabelStatus.Duplicate);
      }

      optionValue.Value = label;

      try
      {
        await _context.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException exception)
          when (exception.InnerException is PostgresException
          {
            SqlState: "23505",
            ConstraintName: "IX_product_option_values_OptionId_Value"
          })
      {
        return new(RenameProductLabelStatus.Duplicate);
      }

      await transaction.CommitAsync(cancellationToken);

      return new(
          RenameProductLabelStatus.Updated,
          optionValue.Value);
    }

    private async Task<Product?> FindAndLockOwnedProductAsync(
    Guid ownerId,
    Guid productId,
    CancellationToken cancellationToken)
    {
      if (ownerId == Guid.Empty || productId == Guid.Empty)
      {
        return null;
      }

      var products = await _context.Products
          .FromSqlInterpolated(
              $"SELECT * FROM products WHERE \"Id\" = {productId} FOR UPDATE")
          .AsTracking()
          .ToListAsync(cancellationToken);

      var product = products.SingleOrDefault();

      if (product == null)
      {
        return null;
      }

      var canAccessStore = await _context.Stores
          .AnyAsync(
              store =>
                  store.Id == product.StoreId &&
                  store.OwnerId == ownerId &&
                  store.Owner.IsVerified &&
                  store.Owner.Role == UserRole.Seller,
              cancellationToken);

      return canAccessStore ? product : null;
    }

    private static ProductDetailResponse ToResponse(Product product)
    {
      var options = product.Options
          .OrderBy(option => option.SortOrder)
          .ThenBy(option => option.Id)
          .Select(option => new ProductOptionResponse(
              option.Id,
              option.Name,
              option.SortOrder,
              option.Values
                  .OrderBy(value => value.SortOrder)
                  .ThenBy(value => value.Id)
                  .Select(value => new ProductOptionValueResponse(
                      value.Id,
                      value.Value,
                      value.SortOrder,
                      value.ImageId))
                  .ToList()))
          .ToList();

      var variants = product.Variants
          .OrderBy(variant => variant.Sku)
          .Select(variant => new ProductVariantResponse(
              variant.Id,
              variant.Sku,
              variant.OptionValues
                  .OrderBy(link => link.OptionId)
                  .Select(link => link.OptionValueId)
                  .ToList(),
              variant.PriceAdjustment,
              product.BasePrice + variant.PriceAdjustment,
              variant.StockQuantity))
          .ToList();

      return new ProductDetailResponse(
          product.Id,
          product.StoreId,
          product.CategoryId,
          product.Title,
          product.Slug,
          product.Description,
          product.WeightGrams,
          product.BasePrice,
          product.IsActive,
          product.CreatedAt,
          options,
          variants);
    }
  }
}