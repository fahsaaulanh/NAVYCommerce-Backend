using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Stores
{
    public class CreateStoreRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Nama toko wajib diisi.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage = "Nama toko harus terdiri dari 2–150 karakter.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug toko wajib diisi.")]
        [StringLength(
            160,
            ErrorMessage = "Slug maksimal 160 karakter.")]
        [RegularExpression(
            @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage =
                "Slug hanya boleh berisi huruf kecil, angka, " +
                "dan tanda hubung di antara kata.")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(
            2000,
            ErrorMessage = "Deskripsi maksimal 2000 karakter.")]
        public string? Description { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(Name) &&
                Name.Trim().Length < 2)
            {
                yield return new ValidationResult(
                    "Nama toko minimal 2 karakter setelah spasi tepi dihapus.",
                    new[] { nameof(Name) });
            }
        }

    }

    public sealed record StoreResponse(
           Guid Id,
           string Name,
           string Slug,
           string? Description,
           DateTime CreatedAt);

    public class StoreListRequest
    {
        [Range(
            1,
            1_000_000,
            ErrorMessage = "Page harus antara 1 dan 1000000.")]
        public int Page { get; set; } = 1;

        [Range(
            1,
            100,
            ErrorMessage = "PageSize harus antara 1 dan 100.")]
        public int PageSize { get; set; } = 20;
    }

    public sealed record StoreListResponse(
        IReadOnlyList<StoreResponse> Data,
        int Page,
        int PageSize,
        int Total)
    {
        public int TotalPages =>
            (int)Math.Ceiling(Total / (double)PageSize);
    }
}