using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed class UploadProductImageRequest
  {
    [Required(ErrorMessage = "File gambar wajib diisi.")]
    public IFormFile File { get; set; } = null!;

    [StringLength(
        250,
        ErrorMessage = "Teks alternatif maksimal 250 karakter.")]
    public string? AltText { get; set; }
  }
}