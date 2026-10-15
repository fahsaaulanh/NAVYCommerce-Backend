using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed class RenameProductOptionRequest
  {
    [Required(ErrorMessage = "Nama opsi wajib diisi.")]
    [StringLength(
        50,
        ErrorMessage = "Nama opsi maksimal 50 karakter.")]
    public string Name { get; init; } = string.Empty;
  }
}