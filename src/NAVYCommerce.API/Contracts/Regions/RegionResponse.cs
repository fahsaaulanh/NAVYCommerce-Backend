namespace NAVYCommerce.API.Contracts.Regions;

public sealed record RegionResponse(
    string Code,
    string Name,
    string? PostalCode = null);
