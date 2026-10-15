using System.ComponentModel.DataAnnotations;
using System.Text;
using NAVYCommerce.Domain.Entities;
using System.Text.RegularExpressions;

namespace NAVYCommerce.API.Contracts.Auth
{
    public class RegisterRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(254, ErrorMessage = "Email maksimal 254 karakter.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi.")]
        [StringLength(
            72,
            MinimumLength = 10,
            ErrorMessage = "Password harus terdiri dari 10–72 karakter.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage = "Nama lengkap harus terdiri dari 2–100 karakter.")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(
            30,
            ErrorMessage = "Nomor telepon maksimal 30 karakter.")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Role wajib diisi.")]
        [EnumDataType(
            typeof(UserRole),
            ErrorMessage = "Role tidak valid.")]
        public UserRole? Role { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (Role.HasValue &&
                Role.Value != UserRole.Customer &&
                Role.Value != UserRole.Seller)
            {
                yield return new ValidationResult(
                    "Registrasi hanya tersedia untuk Customer atau Seller.",
                    new[] { nameof(Role) });
            }

            if (!string.IsNullOrEmpty(Password))
            {
                if (!Regex.IsMatch(Password, "[A-Z]"))
                {
                    yield return new ValidationResult(
                        "Password harus mengandung minimal satu huruf kapital A–Z.",
                        new[] { nameof(Password) });
                }

                if (!Regex.IsMatch(
                    Password,
                    @"[\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E]"))
                {
                    yield return new ValidationResult(
                        "Password harus mengandung minimal satu simbol, misalnya !, @, atau #.",
                        new[] { nameof(Password) });
                }
            }

            if (!string.IsNullOrEmpty(Password) &&
                Encoding.UTF8.GetByteCount(Password) > 72)
            {
                yield return new ValidationResult(
                    "Password maksimal 72 byte UTF-8.",
                    new[] { nameof(Password) });
            }

            if (!string.IsNullOrWhiteSpace(FullName) &&
                FullName.Trim().Length < 2)
            {
                yield return new ValidationResult(
                    "Nama lengkap minimal 2 karakter setelah spasi tepi dihapus.",
                    new[] { nameof(FullName) });
            }
        }
    }

    public class LoginRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(254, ErrorMessage = "Email maksimal 254 karakter.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi.")]
        public string Password { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(Password) &&
                Encoding.UTF8.GetByteCount(Password) > 72)
            {
                yield return new ValidationResult(
                    "Password maksimal 72 byte UTF-8.",
                    new[] { nameof(Password) });
            }
        }
    }

    public class VerifyOtpRequest
    {
        [Required(ErrorMessage = "Email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(254, ErrorMessage = "Email maksimal 254 karakter.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP wajib diisi.")]
        [RegularExpression(
            @"^[0-9]{6}$",
            ErrorMessage = "OTP harus terdiri dari 6 digit angka.")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ResendOtpRequest
    {
        [Required(ErrorMessage = "Email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(254, ErrorMessage = "Email maksimal 254 karakter.")]
        public string Email { get; set; } = string.Empty;
    }
}