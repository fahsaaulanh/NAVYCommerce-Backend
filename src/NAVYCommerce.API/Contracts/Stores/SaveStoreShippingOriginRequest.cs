using System.ComponentModel.DataAnnotations;
using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Contracts.Stores;

public sealed class SaveStoreShippingOriginRequest
{
  [Required(ErrorMessage = "Nama kontak pengiriman wajib diisi.")]
  [StringLength(150, ErrorMessage = "Nama kontak maksimal 150 karakter.")]
  public string ContactName { get; init; } = string.Empty;

  [Required(ErrorMessage = "Nomor telepon wajib diisi.")]
  [RegularExpression(@"^\+?[0-9]{8,15}$",
      ErrorMessage = "Nomor telepon harus berisi 8–15 digit, boleh diawali +, tanpa spasi.")]
  public string PhoneNumber { get; init; } = string.Empty;

  [Required(ErrorMessage = "Kelurahan atau desa wajib dipilih.")]
  [StringLength(20, ErrorMessage = "Kode wilayah maksimal 20 karakter.")]
  [RegularExpression(RegionCodePatterns.Village,
      ErrorMessage = "Format kode kelurahan atau desa tidak valid.")]
  public string VillageCode { get; init; } = string.Empty;

  [Required(ErrorMessage = "Kode pos wajib diisi.")]
  [RegularExpression(@"^[0-9]{5}$",
      ErrorMessage = "Kode pos harus terdiri dari 5 digit.")]
  public string PostalCode { get; init; } = string.Empty;

  [Required(ErrorMessage = "Alamat asal pengiriman wajib diisi.")]
  [StringLength(500, ErrorMessage = "Alamat maksimal 500 karakter.")]
  public string AddressLine { get; init; } = string.Empty;

  [StringLength(250, ErrorMessage = "Catatan penjemputan maksimal 250 karakter.")]
  public string? PickupNotes { get; init; }
}
