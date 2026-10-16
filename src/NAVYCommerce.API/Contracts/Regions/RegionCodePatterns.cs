namespace NAVYCommerce.API.Contracts.Regions;

public static class RegionCodePatterns
{
  public const string Province = @"^[0-9]{2}$";
  public const string City = @"^[0-9]{2}\.[0-9]{2}$";
  public const string District = @"^[0-9]{2}\.[0-9]{2}\.[0-9]{2}$";
  public const string Village = @"^[0-9]{2}\.[0-9]{2}\.[0-9]{2}\.[0-9]{4}$";
}
