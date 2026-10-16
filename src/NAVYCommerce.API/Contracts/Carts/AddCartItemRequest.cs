using System;
using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Carts
{
  public sealed class AddCartItemRequest
  {
    [Required]
    public Guid? ProductVariantId { get; init; }

    [Required(ErrorMessage = "Jumlah wajib diisi.")]
    [Range(1, int.MaxValue, ErrorMessage = "Jumlah harus lebih dari 0.")]
    public int? Quantity { get; init; }
  }
}