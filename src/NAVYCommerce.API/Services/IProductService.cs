using System;
using System.Threading;
using System.Threading.Tasks;
using NAVYCommerce.API.Contracts.Products;

namespace NAVYCommerce.API.Services
{
    public enum CreateProductStatus
    {
        Created,
        StoreNotFound,
        CategoryNotFound,
        SlugAlreadyUsed
    }

    public record CreateProductResult(
        CreateProductStatus Status,
        ProductDetailResponse? Product = null);

    public enum UpdateProductVariantStatus
    {
        Updated,
        NotFound,
        InvalidPrice
    }

    public record UpdateProductVariantResult(
        UpdateProductVariantStatus Status,
        ProductVariantResponse? Variant = null);

    public interface IProductService
    {
        Task<CreateProductResult> CreateAsync(
            Guid ownerId,
            CreateProductRequest request,
            CancellationToken cancellationToken = default);

        Task<ProductDetailResponse?> GetOwnedProductAsync(
            Guid ownerId,
            Guid productId,
            CancellationToken cancellationToken = default);

        Task<ProductListResponse> GetPublicProductsAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<UpdateProductVariantResult> UpdateVariantAsync(
            Guid ownerId,
            Guid productId,
            Guid variantId,
            UpdateProductVariantRequest request,
            CancellationToken cancellationToken = default);

        Task<SellerProductListResponse?> GetOwnedByStoreAsync(
            Guid ownerId,
            Guid storeId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}