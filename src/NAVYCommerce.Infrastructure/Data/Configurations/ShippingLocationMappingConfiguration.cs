using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations;

public sealed class ShippingLocationMappingConfiguration
    : IEntityTypeConfiguration<ShippingLocationMapping>
{
    public void Configure(EntityTypeBuilder<ShippingLocationMapping> builder)
    {
        builder.ToTable("shipping_location_mappings");
        builder.HasKey(x => new { x.Provider, x.RegionFingerprint });
        builder.Property(x => x.Provider).HasMaxLength(30);
        builder.Property(x => x.RegionFingerprint).HasMaxLength(64);
        builder.Property(x => x.DestinationLabel).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DestinationId).IsRequired();
        builder.Property(x => x.ConfirmedAt).IsRequired();
    }
}
