using System;
using System.Collections.Generic;

namespace NAVYCommerce.Domain.Entities
{
  public class User
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; }
    public ICollection<UserOtp> Otps { get; set; } = new List<UserOtp>();
    public ICollection<Store> Stores { get; set; } = new List<Store>();
    public Cart? Cart { get; set; }
    public ICollection<CustomerAddress> Addresses { get; set; }
    = new List<CustomerAddress>();
  }

  public enum UserRole
  {
    Customer,
    Seller,
    SuperAdmin

  }

}