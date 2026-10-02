using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Services
{
  public enum RegistrationOtpStatus
  {
    Sent,
    Verified,
    UserNotFound,
    AlreadyVerified,
    Cooldown,
    InvalidCode,
    Expired,
    AttemptsExceeded
  }

  public sealed record RegistrationOtpResult(
      RegistrationOtpStatus Status,
      string? OtpCode = null,
      DateTime? ExpiresAt = null,
      int? RetryAfterSeconds = null);

  public interface IRegistrationOtpService
  {
    Task<RegistrationOtpResult> ResendAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<RegistrationOtpResult> VerifyAsync(
        string email,
        string otpCode,
        CancellationToken cancellationToken = default);

    UserOtp CreateRegistrationOtp(Guid userId);
  }

  public class RegistrationOtpService : IRegistrationOtpService
  {
    private static readonly TimeSpan OtpLifetime =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ResendCooldown =
        TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _context;

    public RegistrationOtpService(ApplicationDbContext context)
    {
      _context = context;
    }

    private const int MaxFailedAttempts = 5;

    public UserOtp CreateRegistrationOtp(Guid userId)
    {
      var now = DateTime.UtcNow;

      return new UserOtp
      {
        UserId = userId,
        OtpCode = RandomNumberGenerator
              .GetInt32(100000, 1000000)
              .ToString(),
        Type = OtpType.Register,
        CreatedAt = now,
        ExpiresAt = now.Add(OtpLifetime),
        IsUsed = false,
        FailedAttempts = 0
      };
    }

    public async Task<RegistrationOtpResult> ResendAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var user = await FindAndLockUserAsync(
          email, cancellationToken);

      if (user == null)
      {
        return new(RegistrationOtpStatus.UserNotFound);
      }

      if (user.IsVerified)
      {
        return new(RegistrationOtpStatus.AlreadyVerified);
      }

      var now = DateTime.UtcNow;

      var latestOtp = await _context.UserOtps
          .AsNoTracking()
          .Where(otp =>
              otp.UserId == user.Id &&
              otp.Type == OtpType.Register)
          .OrderByDescending(otp => otp.CreatedAt)
          .FirstOrDefaultAsync(cancellationToken);

      if (latestOtp != null)
      {
        var nextAllowedAt =
            latestOtp.CreatedAt.Add(ResendCooldown);

        if (now < nextAllowedAt)
        {
          var remainingSeconds = (int)Math.Ceiling(
              (nextAllowedAt - now).TotalSeconds);

          return new(
              RegistrationOtpStatus.Cooldown,
              RetryAfterSeconds: remainingSeconds);
        }
      }

      await _context.UserOtps
          .Where(otp =>
              otp.UserId == user.Id &&
              otp.Type == OtpType.Register &&
              !otp.IsUsed)
          .ExecuteUpdateAsync(
              setters => setters.SetProperty(
                  otp => otp.IsUsed, true),
              cancellationToken);

      UserOtp newOtp;

      do
      {
        newOtp = CreateRegistrationOtp(user.Id);
      }
      while (newOtp.OtpCode == latestOtp?.OtpCode);

      _context.UserOtps.Add(newOtp);

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return new(
          RegistrationOtpStatus.Sent,
          OtpCode: newOtp.OtpCode,
          ExpiresAt: newOtp.ExpiresAt);
    }

    public async Task<RegistrationOtpResult> VerifyAsync(
    string email,
    string otpCode,
    CancellationToken cancellationToken = default)
    {
      await using var transaction =
          await _context.Database.BeginTransactionAsync(
              cancellationToken);

      var user = await FindAndLockUserAsync(
          email, cancellationToken);

      if (user == null)
      {
        return new(RegistrationOtpStatus.UserNotFound);
      }

      if (user.IsVerified)
      {
        return new(RegistrationOtpStatus.AlreadyVerified);
      }

      var latestOtp = await _context.UserOtps
          .Where(otp =>
              otp.UserId == user.Id &&
              otp.Type == OtpType.Register)
          .OrderByDescending(otp => otp.CreatedAt)
          .FirstOrDefaultAsync(cancellationToken);

      if (latestOtp == null)
      {
        return new(RegistrationOtpStatus.InvalidCode);
      }

      if (latestOtp.FailedAttempts >= MaxFailedAttempts)
      {
        return new(RegistrationOtpStatus.AttemptsExceeded);
      }

      if (latestOtp.IsUsed)
      {
        return new(RegistrationOtpStatus.InvalidCode);
      }

      var now = DateTime.UtcNow;

      if (latestOtp.ExpiresAt <= now)
      {
        return new(RegistrationOtpStatus.Expired);
      }

      if (latestOtp.OtpCode != otpCode)
      {
        latestOtp.FailedAttempts++;

        var limitReached =
            latestOtp.FailedAttempts >= MaxFailedAttempts;

        if (limitReached)
        {
          latestOtp.IsUsed = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            limitReached
                ? RegistrationOtpStatus.AttemptsExceeded
                : RegistrationOtpStatus.InvalidCode);
      }

      user.IsVerified = true;
      user.LastUpdatedAt = now;
      latestOtp.IsUsed = true;

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return new(RegistrationOtpStatus.Verified);
    }

    private Task<User?> FindAndLockUserAsync(
        string email,
        CancellationToken cancellationToken)
    {
      return _context.Users
          .FromSqlInterpolated($@"
                    SELECT *
                    FROM users
                    WHERE ""Email"" = {email}
                    FOR UPDATE")
          .SingleOrDefaultAsync(cancellationToken);
    }
  }
}