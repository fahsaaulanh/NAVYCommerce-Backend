using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class AuthController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
      _context = context;
      _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
      var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

      if (existingUser != null)
      {
        return BadRequest(new
        {
          message = "Email is already registered."
        });
      }

      var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

      var newUser = new User
      {
        Email = request.Email,
        PasswordHash = passwordHash,
        FullName = request.FullName,
        PhoneNumber = request.PhoneNumber,
        Role = request.Role,
        IsVerified = false,
      };

      _context.Users.Add(newUser);

      var random = new Random();
      var otpCode = random.Next(100000, 999999).ToString();

      var userOtp = new UserOtp
      {
        UserId = newUser.Id,
        OtpCode = otpCode,
        Type = OtpType.Register,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        IsUsed = false,
      };

      _context.UserOtps.Add(userOtp);

      await _context.SaveChangesAsync();
      return Ok(new { Message = "User registered successfully. Please verify your email.", MockEmailInbox = $"Your OTP code is: {otpCode}" });
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
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

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
  }

  public class RegisterRequest
  {
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
  }

  public class VerifyOtpRequest
  {
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
  }

  public class LoginRequest
  {
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
  }
}