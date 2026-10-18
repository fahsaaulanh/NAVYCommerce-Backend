using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations;

public sealed class StoreShippingOriginConfiguration
    : IEntityTypeConfiguration<StoreShippingOrigin>
{
  public void Configure(EntityTypeBuilder<StoreShippingOrigin> builder)
  {
    builder.ToTable("store_shipping_origins");
    builder.HasKey(origin => origin.StoreId);
    builder.Property(origin => origin.StoreId).ValueGeneratedNever();

    builder.Property(origin => origin.ContactName).HasMaxLength(150).IsRequired();
    builder.Property(origin => origin.PhoneNumber).HasMaxLength(20).IsRequired();
    builder.Property(origin => origin.ProvinceCode).HasMaxLength(20).IsRequired();
    builder.Property(origin => origin.Province).HasMaxLength(100).IsRequired();
    builder.Property(origin => origin.CityCode).HasMaxLength(20).IsRequired();
    builder.Property(origin => origin.City).HasMaxLength(100).IsRequired();
    builder.Property(origin => origin.DistrictCode).HasMaxLength(20).IsRequired();
    builder.Property(origin => origin.District).HasMaxLength(100).IsRequired();
    builder.Property(origin => origin.VillageCode).HasMaxLength(20).IsRequired();
    builder.Property(origin => origin.Village).HasMaxLength(100).IsRequired();
    builder.Property(origin => origin.PostalCode).HasMaxLength(5).IsRequired();
    builder.Property(origin => origin.AddressLine).HasMaxLength(500).IsRequired();
    builder.Property(origin => origin.PickupNotes).HasMaxLength(250);

    builder.HasOne(origin => origin.Store)
        .WithOne(store => store.ShippingOrigin)
        .HasForeignKey<StoreShippingOrigin>(origin => origin.StoreId)
        .OnDelete(DeleteBehavior.Cascade);
  }
}
