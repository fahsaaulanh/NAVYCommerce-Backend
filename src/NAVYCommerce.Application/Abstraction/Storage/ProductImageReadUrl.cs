using System;

namespace NAVYCommerce.Application.Abstractions.Storage
{
  public sealed record ProductImageReadUrl(
      string Url,
      DateTimeOffset ExpiresAt);
}