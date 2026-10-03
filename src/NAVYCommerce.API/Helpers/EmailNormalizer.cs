namespace NAVYCommerce.API.Helpers
{
  public static class EmailNormalizer
  {
    public static string Normalize(string email)
    {
      return email.Trim().ToLowerInvariant();
    }
  }
}