using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed class RenameProductOptionValueRequest
  {
    [Required(ErrorMessage = "Nilai pilihan wajib diisi.")]
    [StringLength(
        50,
        ErrorMessage = "Nilai pilihan maksimal 50 karakter.")]
    public string Value { get; init; } = string.Empty;
  }
}