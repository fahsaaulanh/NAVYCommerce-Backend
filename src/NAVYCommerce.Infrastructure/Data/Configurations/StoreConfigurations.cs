using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public class StoreConfiguration : IEntityTypeConfiguration<Store>
  {
    public void Configure(EntityTypeBuilder<Store> builder)
    {
      builder.ToTable("stores");

      builder.HasKey(store => store.Id);

      builder.Property(store => store.Name)
          .IsRequired()
          .HasMaxLength(150);

      builder.Property(store => store.Slug)
          .IsRequired()
          .HasMaxLength(160);

      builder.HasIndex(store => store.Slug)
          .IsUnique();

      builder.Property(store => store.Description)
          .HasMaxLength(2000);

      builder.HasOne(store => store.Owner)
          .WithMany(user => user.Stores)
          .HasForeignKey(store => store.OwnerId)
          .OnDelete(DeleteBehavior.Restrict);

      builder.HasMany(store => store.Products)
          .WithOne(product => product.Store)
          .HasForeignKey(product => product.StoreId)
          .OnDelete(DeleteBehavior.Restrict);
    }
  }
}