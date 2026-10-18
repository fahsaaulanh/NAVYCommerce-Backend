using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Carts;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.Application.Abstractions.Storage;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Services.Carts
{
  public sealed partial class CartService : ICartService
  {
    private readonly ApplicationDbContext _context;
    private readonly IProductImageStorage _imageStorage;

    public CartService(
        ApplicationDbContext context,
        IProductImageStorage imageStorage)
    {
      _context = context;
      _imageStorage = imageStorage;
    }

    public async Task<CartResponse?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
      if (!await IsCustomerAsync(customerId, cancellationToken))
      {
        return null;
      }

      var cartItems = await _context.CartItems
          .AsNoTrackingWithIdentityResolution()
          .Where(item => item.Cart.CustomerId == customerId)
          .Include(item => item.ProductVariant)
              .ThenInclude(variant => variant.Product!)
              .ThenInclude(product => product.Store)
              .ThenInclude(store => store.Owner)
          .Include(item => item.ProductVariant)
              .ThenInclude(variant => variant.Product!)
              .ThenInclude(product => product.Images)
          .Include(item => item.ProductVariant)
              .ThenInclude(variant => variant.Product!)
              .ThenInclude(product => product.Options)
              .ThenInclude(option => option.Values)
          .Include(item => item.ProductVariant)
              .ThenInclude(variant => variant.OptionValues)
          .OrderByDescending(item => item.CreatedAt)
          .ThenBy(item => item.Id)
          .AsSplitQuery()
          .ToListAsync(cancellationToken);

      var responses = new List<CartItemResponse>(cartItems.Count);
      var imageCache = new Dictionary<Guid, ProductImageResponse>();

      foreach (var item in cartItems)
      {
        var variant = item.ProductVariant;
        var product = variant.Product
            ?? throw new InvalidOperationException(
                "Varian keranjang tidak memiliki produk.");

        var selectedIds = variant.OptionValues
            .Select(link => link.OptionValueId)
            .ToHashSet();

        var selectedValues = product.Options
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Id)
            .SelectMany(option => option.Values
                .Where(value => selectedIds.Contains(value.Id))
                .OrderBy(value => value.SortOrder)
                .ThenBy(value => value.Id)
                .Select(value => new { Option = option, Value = value }))
            .ToList();

        var options = selectedValues
            .Select(selection => new CartItemOptionResponse(
                selection.Option.Name,
                selection.Value.Value))
            .ToList();

        // Gambar opsi pertama menurut urutan opsi, lalu gambar galeri pertama.
        var selectedImageId = selectedValues
            .Select(selection => selection.Value.ImageId)
            .FirstOrDefault(imageId => imageId.HasValue);

        var image = product.Images
            .FirstOrDefault(candidate => candidate.Id == selectedImageId)
            ?? product.Images
                .OrderBy(candidate => candidate.SortOrder)
                .ThenBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();

        ProductImageResponse? imageResponse = null;

        if (image != null)
        {
          if (!imageCache.TryGetValue(image.Id, out var cachedImage))
          {
            var readUrl = await _imageStorage.CreateReadUrlAsync(
                image.StorageKey,
                cancellationToken);

            cachedImage = new ProductImageResponse(
                image.Id,
                readUrl.Url,
                readUrl.ExpiresAt,
                image.AltText,
                image.SortOrder,
                image.CreatedAt);

            imageCache.Add(image.Id, cachedImage);
          }

          imageResponse = cachedImage;
        }

        var unitPrice = product.BasePrice + variant.PriceAdjustment;
        var canCheckout = ValidateVariant(variant, item.Quantity) == null;

        responses.Add(new CartItemResponse(
            item.Id,
            product.Id,
            variant.Id,
            product.StoreId,
            product.Store.Name,
            product.Title,
            product.Slug,
            variant.Sku,
            options,
            item.Quantity,
            unitPrice,
            unitPrice * item.Quantity,
            variant.StockQuantity,
            canCheckout,
            imageResponse));
      }

      return new CartResponse(
          responses,
          responses.Count,
          responses.Sum(item => (long)item.Quantity),
          responses.Sum(item => item.LineTotal));
    }

    private static CartMutationResult? ValidateVariant(
        ProductVariant variant,
        long quantity) => CartVariantRules.Validate(variant, quantity);
  }
}
