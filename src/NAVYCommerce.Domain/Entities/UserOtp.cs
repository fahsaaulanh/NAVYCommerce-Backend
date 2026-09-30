namespace NAVYCommerce.Domain.Entities
{
  public class UserOtp
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string OtpCode { get; set; } = string.Empty;
    public OtpType Type { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
  }
  public enum OtpType
  {
    Register,
    ResetPassword,
    WithdrawalAuth,
  }
}