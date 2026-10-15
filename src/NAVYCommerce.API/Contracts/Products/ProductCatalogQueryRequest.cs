using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace NAVYCommerce.API.Contracts.Products
{
    public sealed class ProductCatalogQueryRequest : IValidatableObject
    {
        [Range(1, 1_000_000)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        [FromQuery(Name = "q")]
        [StringLength(120, ErrorMessage = "Pencarian maksimal 120 karakter.")]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int? CategoryId { get; set; }

        [Range(typeof(decimal), "0", "999999999999.99")]
        public decimal? MinPrice { get; set; }

        [Range(typeof(decimal), "0", "999999999999.99")]
        public decimal? MaxPrice { get; set; }

        public bool InStock { get; set; }

        [RegularExpression("^(newest|price-asc|price-desc|name)$",
            ErrorMessage = "Urutan produk tidak valid.")]
        public string Sort { get; set; } = "newest";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice.Value > MaxPrice.Value)
            {
                yield return new ValidationResult(
                    "Harga minimum tidak boleh melebihi harga maksimum.",
                    new[] { nameof(MinPrice), nameof(MaxPrice) });
            }
        }
    }
}
