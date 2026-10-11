
using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Products;

public sealed class ReorderProductImagesRequest
{
  [Required(ErrorMessage = "Daftar ID gambar wajib diisi.")]
  [MaxLength(10, ErrorMessage = "Maksimal 10 gambar.")]
  public List<Guid> ImageIds { get; set; } = new();
}