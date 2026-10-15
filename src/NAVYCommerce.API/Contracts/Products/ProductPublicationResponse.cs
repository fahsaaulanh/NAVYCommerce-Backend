using System;

namespace NAVYCommerce.API.Contracts.Products
{
  public sealed record ProductPublicationResponse(
      Guid Id,
      bool IsActive);

  public sealed record ProductPublicationIssue(
      string Code,
      string Message);
}