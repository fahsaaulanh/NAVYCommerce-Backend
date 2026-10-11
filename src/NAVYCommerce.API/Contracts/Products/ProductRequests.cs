using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NAVYCommerce.API.Contracts.Products
{
  public class CreateProductRequest : IValidatableObject
  {
    [Required(ErrorMessage = "Toko wajib dipilih.")]
    public Guid? StoreId { get; set; }

    [Required(ErrorMessage = "Kategori wajib dipilih.")]
    [Range(1, int.MaxValue, ErrorMessage = "Kategori tidak valid.")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Nama produk wajib diisi.")]
    [StringLength(
        150,
        MinimumLength = 2,
        ErrorMessage = "Nama produk harus terdiri dari 2–150 karakter.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug wajib diisi.")]
    [StringLength(160, ErrorMessage = "Slug maksimal 160 karakter.")]
    [RegularExpression(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        ErrorMessage = "Slug hanya boleh berisi huruf kecil, angka, dan tanda hubung.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(
        5000,
        ErrorMessage = "Deskripsi maksimal 5.000 karakter.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Berat produk wajib diisi.")]
    [Range(
        1,
        1_000_000,
        ErrorMessage = "Berat harus antara 1–1.000.000 gram.")]
    public int? WeightGrams { get; set; }

    [Required(ErrorMessage = "Harga produk wajib diisi.")]
    public decimal? BasePrice { get; set; }

    [Required(ErrorMessage = "Options tidak boleh null.")]
    public List<CreateProductOptionRequest?>? Options { get; set; }
        = new();

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
      if (StoreId == Guid.Empty)
      {
        yield return new ValidationResult(
            "ID toko tidak valid.",
            new[] { nameof(StoreId) });
      }

      if ((Title ?? string.Empty).Trim().Length < 2)
      {
        yield return new ValidationResult(
            "Nama produk minimal 2 karakter setelah spasi dibersihkan.",
            new[] { nameof(Title) });
      }

      if (BasePrice.HasValue &&
          (BasePrice.Value <= 0 ||
           BasePrice.Value > 999_999_999_999.99m))
      {
        yield return new ValidationResult(
            "Harga harus lebih dari 0 dan maksimal 999.999.999.999,99.",
            new[] { nameof(BasePrice) });
      }

      if (Options == null)
      {
        yield break;
      }

      if (Options.Count > 3)
      {
        yield return new ValidationResult(
            "Produk maksimal memiliki 3 jenis opsi.",
            new[] { nameof(Options) });

        yield break;
      }

      var names = new HashSet<string>(
          StringComparer.OrdinalIgnoreCase);

      var combinationCount = 1;

      foreach (var option in Options)
      {
        if (option == null)
        {
          yield return new ValidationResult(
              "Opsi tidak boleh berisi item null.",
              new[] { nameof(Options) });

          yield break;
        }

        var name = (option.Name ?? string.Empty).Trim();

        if (!names.Add(name))
        {
          yield return new ValidationResult(
              "Nama opsi tidak boleh duplikat.",
              new[] { nameof(Options) });

          yield break;
        }

        var count = option.Values?.Count ?? 0;

        if (count < 1 || count > 20)
        {
          yield break;
        }

        combinationCount *= count;

        if (combinationCount > 100)
        {
          yield return new ValidationResult(
              "Total kombinasi varian maksimal 100.",
              new[] { nameof(Options) });

          yield break;
        }
      }
    }
  }

  public class CreateProductOptionRequest : IValidatableObject
  {
    [Required(ErrorMessage = "Nama opsi wajib diisi.")]
    [StringLength(
        50,
        ErrorMessage = "Nama opsi maksimal 50 karakter.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pilihan opsi wajib diisi.")]
    public List<string?>? Values { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
      if (Values == null)
      {
        yield break;
      }

      if (Values.Count < 1 || Values.Count > 20)
      {
        yield return new ValidationResult(
            "Setiap opsi harus memiliki 1–20 pilihan.",
            new[] { nameof(Values) });

        yield break;
      }

      if (Values.Any(value =>
          string.IsNullOrWhiteSpace(value) ||
          value.Trim().Length > 50))
      {
        yield return new ValidationResult(
            "Setiap pilihan harus terdiri dari 1–50 karakter.",
            new[] { nameof(Values) });

        yield break;
      }

      var uniqueValues = new HashSet<string>(
          Values.Select(value => value!.Trim()),
          StringComparer.OrdinalIgnoreCase);

      if (uniqueValues.Count != Values.Count)
      {
        yield return new ValidationResult(
            "Pilihan dalam satu opsi tidak boleh duplikat.",
            new[] { nameof(Values) });
      }
    }
  }

  public class UpdateProductVariantRequest
  {
    [Required(ErrorMessage = "Penyesuaian harga wajib diisi.")]
    public decimal? PriceAdjustment { get; set; }

    [Required(ErrorMessage = "Stok wajib diisi.")]
    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "Stok tidak boleh negatif.")]
    public int? StockQuantity { get; set; }
  }
}