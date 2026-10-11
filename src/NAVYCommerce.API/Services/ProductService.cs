using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.Application.Abstractions.Storage;

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
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
      var query = _context.Products
          .AsNoTracking()
          .Where(product =>
              product.IsActive &&
              product.Store.Owner.IsVerified &&
              product.Store.Owner.Role == UserRole.Seller);

      var total = await query.CountAsync(cancellationToken);

      var products = await query
          .OrderByDescending(product => product.CreatedAt)
          .ThenBy(product => product.Id)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(product => new ProductSummaryResponse(
              product.Id,
              product.StoreId,
              product.CategoryId,
              product.Title,
              product.Slug,
              product.BasePrice))
          .ToListAsync(cancellationToken);

      return new ProductListResponse(
          products,
          page,
          pageSize,
          total);
    }

    public async Task<UpdateProductVariantResult> UpdateVariantAsync(
        Guid ownerId,
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
      var variant = await _context.ProductVariants
          .Include(variant => variant.Product)
          .Include(variant => variant.OptionValues)
          .SingleOrDefaultAsync(
              variant =>
                  variant.Id == variantId &&
                  variant.ProductId == productId &&
                  variant.Product!.Store.OwnerId == ownerId &&
                  variant.Product.Store.Owner.IsVerified &&
                  variant.Product.Store.Owner.Role == UserRole.Seller,
              cancellationToken);

      if (variant == null)
      {
        return new(UpdateProductVariantStatus.NotFound);
      }

      var basePrice = variant.Product!.BasePrice;
      var adjustment = request.PriceAdjustment!.Value;

      const decimal maxPrice = 999_999_999_999.99m;

      // Bandingkan batas sebelum menjumlahkan untuk menghindari overflow.
      if (basePrice < 0 ||
          basePrice > maxPrice ||
          adjustment < -basePrice ||
          adjustment > maxPrice - basePrice)
      {
        return new(UpdateProductVariantStatus.InvalidPrice);
      }

      variant.PriceAdjustment = adjustment;
      variant.StockQuantity = request.StockQuantity!.Value;

      await _context.SaveChangesAsync(cancellationToken);

      var response = new ProductVariantResponse(
          variant.Id,
          variant.Sku,
          variant.OptionValues
              .OrderBy(link => link.OptionId)
              .Select(link => link.OptionValueId)
              .ToList(),
          variant.PriceAdjustment,
          basePrice + variant.PriceAdjustment,
          variant.StockQuantity);

      return new(
          UpdateProductVariantStatus.Updated,
          response);
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