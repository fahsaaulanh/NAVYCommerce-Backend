namespace NAVYCommerce.API.Services.Shipping.RajaOngkir;

public sealed class RajaOngkirOptions
{
    public const string SectionName = "Shipping:RajaOngkir";
    public string BaseUrl { get; set; } = "https://rajaongkir.komerce.id/api/v1/";
    public string ApiKey { get; set; } = string.Empty;
}
