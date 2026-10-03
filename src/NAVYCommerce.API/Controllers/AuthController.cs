using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.API.Contracts.Auth;
using NAVYCommerce.API.Services;
using Npgsql;
using NAVYCommerce.API.Helpers;
namespace NAVYCommerce.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class AuthController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IRegistrationOtpService _registrationOtpService;
    private readonly IWebHostEnvironment _environment;
    public AuthController(ApplicationDbContext context,
    IConfiguration configuration,
    IRegistrationOtpService registrationOtpService,
    IWebHostEnvironment environment)
    {
      _context = context;
      _configuration = configuration;
      _registrationOtpService = registrationOtpService;
      _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
    [FromBody] RegisterRequest request,
    CancellationToken cancellationToken)
    {
      var normalizedEmail = EmailNormalizer.Normalize(request.Email);
      var existingUser = await _context.Users
          .AsNoTracking()
          .FirstOrDefaultAsync(
              user => user.Email == normalizedEmail,
              cancellationToken);

      if (existingUser != null)
      {
        if (!existingUser.IsVerified)
        {
          return Conflict(new
          {
            code = "AUTH_VERIFICATION_REQUIRED",
            message =
                  "Email sudah terdaftar tetapi belum terverifikasi. " +
                  "Silakan meminta OTP baru."
          });
        }

        return Conflict(new
        {
          code = "AUTH_EMAIL_ALREADY_REGISTERED",
          message = "Email sudah terdaftar. Silakan login."
        });
      }

      var newUser = new User
      {
        Email = normalizedEmail,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(
              request.Password),
        FullName = request.FullName.Trim(),
        PhoneNumber = string.IsNullOrWhiteSpace(
              request.PhoneNumber)
                  ? null
                  : request.PhoneNumber.Trim(),
        Role = request.Role!.Value,
        IsVerified = false
      };

      var otp = _registrationOtpService
          .CreateRegistrationOtp(newUser.Id);

      _context.Users.Add(newUser);
      _context.UserOtps.Add(otp);

      try
      {
        await _context.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException exception)
          when (exception.InnerException is PostgresException
          {
            SqlState: "23505",
            ConstraintName: "IX_users_Email"
          })
      {
        return Conflict(new
        {
          code = "AUTH_EMAIL_ALREADY_REGISTERED",
          message =
                "Email sudah terdaftar. Silakan login atau lanjutkan verifikasi."
        });
      }

      return Ok(new
      {
        code = "AUTH_REGISTRATION_PENDING",
        message =
              "Registrasi berhasil. Silakan verifikasi email.",
        expiresAt = otp.ExpiresAt,
        mockEmailInbox = _environment.IsDevelopment()
              ? $"Your OTP code is: {otp.OtpCode}"
              : null
      });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
      if (user == null)
      {
        return NotFound(new
        {
          message = "User not found."
        });
      }

      var validOtp = await _context.UserOtps
      .Where(o => o.UserId == user.Id && o.OtpCode == request.OtpCode && o.Type == OtpType.Register)
      .OrderByDescending(o => o.CreatedAt)
      .FirstOrDefaultAsync();

      if (validOtp == null)
      {
        return BadRequest(new
        {
          messag = "Invalid OTP code."
        });
      }

      if (validOtp.IsUsed)
      {
        return BadRequest(new
        {
          message = "This OTP code has already been used."
        });
      }

      if (validOtp.ExpiresAt < DateTime.UtcNow)
      {
        return BadRequest(new
        {
          message = "This OTP code has expired."
        });
      }

      user.IsVerified = true;
      validOtp.IsUsed = true;

      await _context.SaveChangesAsync();
      return Ok(new
      {
        message = "Email verified successfully."
      });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
      var normalizedEmail = EmailNormalizer.Normalize(request.Email);
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

      if (user == null)
      {
        return Unauthorized(new
        {
          message = "Invalid email or password."
        });
      }

      bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
      if (!isPasswordValid)
      {
        return Unauthorized(new
        {
          message = "Invalid email or password."
        });
      }

      if (!user.IsVerified)
      {
        return Unauthorized(new
        {
          message = "Email is not verified. Please verify your email before logging in."
        });
      }

      var tokenHandler = new JwtSecurityTokenHandler();
      var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"]!);

      var tokenDescriptor = new SecurityTokenDescriptor
      {
        Subject = new ClaimsIdentity(new[]
        {
          new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
          new Claim(JwtRegisteredClaimNames.Email, user.Email),
          new Claim("FullName", user.FullName),
          new Claim("Role", user.Role.ToString())
        }),
        Expires = DateTime.UtcNow.AddHours(2),
        Issuer = _configuration["JwtSettings:Issuer"],
        Audience = _configuration["JwtSettings:Audience"],
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
      };

      var token = tokenHandler.CreateToken(tokenDescriptor);
      var jwtString = tokenHandler.WriteToken(token);

      return Ok(new
      {
        Message = "Login successful",
        Token = jwtString,
        Role = user.Role.ToString(),
      });
    }


    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(
      [FromBody] ResendOtpRequest request,
      CancellationToken cancellationToken)
    {
      var result = await _registrationOtpService.ResendAsync(
          request.Email,
          cancellationToken);

      switch (result.Status)
      {
        case RegistrationOtpStatus.Sent:
          return Ok(new
          {
            code = "AUTH_OTP_SENT",
            message = "Kode verifikasi baru telah diterbitkan.",
            expiresAt = result.ExpiresAt,

            mockEmailInbox = _environment.IsDevelopment()
                  ? $"Your OTP code is: {result.OtpCode}"
                  : null
          });

        case RegistrationOtpStatus.Cooldown:
          Response.Headers["Retry-After"] =
              result.RetryAfterSeconds!.Value.ToString();

          return StatusCode(429, new
          {
            code = "AUTH_OTP_COOLDOWN",
            message = "Tunggu sebelum meminta kode baru.",
            retryAfterSeconds = result.RetryAfterSeconds
          });

        case RegistrationOtpStatus.AlreadyVerified:
          return Conflict(new
          {
            code = "AUTH_ALREADY_VERIFIED",
            message = "Akun sudah terverifikasi. Silakan login."
          });

        case RegistrationOtpStatus.UserNotFound:
          return NotFound(new
          {
            code = "AUTH_USER_NOT_FOUND",
            message = "Akun belum terdaftar."
          });

        case RegistrationOtpStatus.AttemptsExceeded:
          return BadRequest(new
          {
            code = "AUTH_OTP_ATTEMPTS_EXCEEDED",
            message =
                  "Batas percobaan OTP tercapai. Silakan meminta kode baru."
          });

        default:
          throw new InvalidOperationException(
              "Hasil penerbitan OTP tidak dikenali.");
      }
    }

  }
}