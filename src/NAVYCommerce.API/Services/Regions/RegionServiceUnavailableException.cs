using System;

namespace NAVYCommerce.API.Services.Regions;

public sealed class RegionServiceUnavailableException : Exception
{
  public RegionServiceUnavailableException(Exception innerException)
      : base("Data wilayah belum dapat dimuat.", innerException)
  {
  }
}
