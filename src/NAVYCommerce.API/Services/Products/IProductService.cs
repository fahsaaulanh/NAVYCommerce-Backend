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
            ProductCatalogQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<System.Collections.Generic.IReadOnlyList<CatalogCategoryResponse>>
            GetCatalogCategoriesAsync(CancellationToken cancellationToken = default);

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

        Task<SetProductPublicationResult> SetPublicationAsync(
            Guid ownerId,
            Guid productId,
            bool isActive,
            CancellationToken cancellationToken = default);

        Task<RenameProductLabelResult> RenameOptionAsync(
            Guid ownerId,
            Guid productId,
            Guid optionId,
            string name,
            CancellationToken cancellationToken = default);

        Task<RenameProductLabelResult> RenameOptionValueAsync(
            Guid ownerId,
            Guid productId,
            Guid optionValueId,
            string value,
            CancellationToken cancellationToken = default);

        Task<PublicProductDetailResponse?> GetPublicProductAsync(
            Guid productId,
            CancellationToken cancellationToken = default);
    }

    public enum SetProductPublicationStatus
    {
        Updated,
        NotFound,
        NotReady
    }

    public sealed record SetProductPublicationResult(
        SetProductPublicationStatus Status,
        ProductPublicationResponse? Product = null,
        System.Collections.Generic.IReadOnlyList<ProductPublicationIssue>?
            Issues = null);

    public enum RenameProductLabelStatus
    {
        Updated,
        NotFound,
        Duplicate,
        InvalidLabel
    }

    public sealed record RenameProductLabelResult(
        RenameProductLabelStatus Status,
        string? Label = null);
}