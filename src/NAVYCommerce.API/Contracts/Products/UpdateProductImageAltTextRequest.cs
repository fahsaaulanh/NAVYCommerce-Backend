using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Products;

public sealed class UpdateProductImageAltTextRequest
{
  [Required(ErrorMessage = "Deskripsi gambar wajib diisi.")]
  [StringLength(
      250,
      ErrorMessage = "Deskripsi gambar maksimal 250 karakter.")]
  public string AltText { get; set; } = string.Empty;
}