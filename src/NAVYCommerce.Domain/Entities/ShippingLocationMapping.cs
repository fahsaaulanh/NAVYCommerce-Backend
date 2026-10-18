namespace NAVYCommerce.Domain.Entities;

public sealed class ShippingLocationMapping
{
    public string Provider { get; set; } = string.Empty;
    public string RegionFingerprint { get; set; } = string.Empty;
    public int DestinationId { get; set; }
    public string DestinationLabel { get; set; } = string.Empty;
    public DateTime ConfirmedAt { get; set; }
}
