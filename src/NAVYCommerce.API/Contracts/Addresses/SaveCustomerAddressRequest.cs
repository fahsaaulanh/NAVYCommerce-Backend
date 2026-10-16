using System.ComponentModel.DataAnnotations;
using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Contracts.Addresses
{
  public sealed class SaveCustomerAddressRequest
  {
    [Required(ErrorMessage = "Label alamat wajib diisi.")]
    [StringLength(50, ErrorMessage = "Label maksimal 50 karakter.")]
    public string Label { get; init; } = string.Empty;

    [Required(ErrorMessage = "Nama penerima wajib diisi.")]
    [StringLength(
        150,
        ErrorMessage = "Nama penerima maksimal 150 karakter.")]
    public string RecipientName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Nomor telepon wajib diisi.")]
    [RegularExpression(
        @"^\+?[0-9]{8,15}$",
        ErrorMessage =
            "Nomor telepon harus berisi 8–15 digit, boleh diawali +, tanpa spasi.")]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kelurahan atau desa wajib dipilih.")]
    [StringLength(
        20,
        ErrorMessage = "Kode wilayah maksimal 20 karakter.")]
    [RegularExpression(
      RegionCodePatterns.Village,
      ErrorMessage = "Format kode kelurahan atau desa tidak valid.")]
    public string VillageCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kode pos wajib diisi.")]
    [RegularExpression(
        @"^[0-9]{5}$",
        ErrorMessage = "Kode pos harus terdiri dari 5 digit.")]
    public string PostalCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Alamat lengkap wajib diisi.")]
    [StringLength(
        500,
        ErrorMessage = "Alamat lengkap maksimal 500 karakter.")]
    public string AddressLine { get; init; } = string.Empty;

    [StringLength(
        250,
        ErrorMessage = "Catatan pengiriman maksimal 250 karakter.")]
    public string? DeliveryNotes { get; init; }
  }
}