using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public sealed class CustomerAddressConfiguration
      : IEntityTypeConfiguration<CustomerAddress>
  {
    public void Configure(
        EntityTypeBuilder<CustomerAddress> builder)
    {
      builder.ToTable("customer_addresses");

      builder.HasKey(address => address.Id);

      builder.Property(address => address.Label)
          .HasMaxLength(50)
          .IsRequired();

      builder.Property(address => address.RecipientName)
          .HasMaxLength(150)
          .IsRequired();

      builder.Property(address => address.PhoneNumber)
          .HasMaxLength(20)
          .IsRequired();

      builder.Property(address => address.Province)
          .HasMaxLength(100)
          .IsRequired();

      builder.Property(address => address.City)
          .HasMaxLength(100)
          .IsRequired();

      builder.Property(address => address.District)
          .HasMaxLength(100)
          .IsRequired();

      builder.Property(address => address.Village)
          .HasMaxLength(100);

      builder.Property(address => address.PostalCode)
          .HasMaxLength(5)
          .IsRequired();

      builder.Property(address => address.AddressLine)
          .HasMaxLength(500)
          .IsRequired();

      builder.Property(address => address.DeliveryNotes)
          .HasMaxLength(250);

      builder.Property(address => address.IsDefault)
          .HasDefaultValue(false);

      builder.HasOne(address => address.Customer)
          .WithMany(customer => customer.Addresses)
          .HasForeignKey(address => address.CustomerId)
          .OnDelete(DeleteBehavior.Cascade);

      builder.HasIndex(
        address => address.CustomerId,
        "IX_customer_addresses_CustomerId");

      builder.HasIndex(
              address => address.CustomerId,
              "IX_customer_addresses_OneDefaultPerCustomer")
          .IsUnique()
          .HasFilter("\"IsDefault\" = TRUE");

      builder.Property(address => address.ProvinceCode)
          .HasMaxLength(20)
          .IsRequired();

      builder.Property(address => address.CityCode)
          .HasMaxLength(20)
          .IsRequired();

      builder.Property(address => address.DistrictCode)
          .HasMaxLength(20)
          .IsRequired();

      builder.Property(address => address.VillageCode)
          .HasMaxLength(20)
          .IsRequired();
    }
  }
}