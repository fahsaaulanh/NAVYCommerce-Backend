namespace NAVYCommerce.API.Contracts.Products
{
    public sealed record CatalogCategoryResponse(
        int Id,
        int? ParentId,
        string Name,
        string Slug,
        int ProductCount);
}
