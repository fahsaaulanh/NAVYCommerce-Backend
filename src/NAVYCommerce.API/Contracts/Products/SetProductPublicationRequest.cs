using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed class SetProductPublicationRequest
  {
    [Required(ErrorMessage = "Status publikasi wajib diisi.")]
    public bool? IsActive { get; init; }
  }
}