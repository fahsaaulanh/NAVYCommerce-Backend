using System;
using System.Security.Claims;

namespace NAVYCommerce.API.Extensions
{
  public static class ClaimsPrincipalExtensions
  {
    public static bool TryGetUserId(
        this ClaimsPrincipal principal,
        out Guid userId)
    {
      var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? principal.FindFirstValue("sub");

      return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
  }
}
