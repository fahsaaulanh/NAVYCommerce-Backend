using System.ComponentModel.DataAnnotations;

namespace NAVYCommerce.API.Contracts.Checkout;

public sealed class CheckoutShippingRequest : IValidatableObject
{
    public Guid AddressId { get; init; }

    [Required(ErrorMessage = "Pilih item keranjang.")]
    [MinLength(1, ErrorMessage = "Pilih minimal satu item keranjang.")]
    [MaxLength(100, ErrorMessage = "Maksimal 100 item keranjang dalam satu permintaan.")]
    public Guid[] CartItemIds { get; init; } = Array.Empty<Guid>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AddressId == Guid.Empty)
            yield return new("Alamat harus dipilih.", new[] { nameof(AddressId) });
        if (CartItemIds == null) yield break;
        if (CartItemIds.Any(id => id == Guid.Empty) ||
            CartItemIds.Distinct().Count() != CartItemIds.Length)
            yield return new("ID item keranjang harus valid dan tidak boleh berulang.",
                new[] { nameof(CartItemIds) });
    }
}
