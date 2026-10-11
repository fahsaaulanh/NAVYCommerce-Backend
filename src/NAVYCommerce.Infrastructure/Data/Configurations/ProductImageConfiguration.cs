using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public class ProductImageConfiguration
      : IEntityTypeConfiguration<ProductImage>
  {
    public void Configure(EntityTypeBuilder<ProductImage> entity)
    {
      entity.ToTable("product_images");

      entity.HasKey(image => image.Id);

      entity.HasAlternateKey(image => new
      {
        image.ProductId,
        image.Id
      });

      entity.Property(image => image.StorageKey)
          .HasMaxLength(500)
          .IsRequired();

      entity.Property(image => image.AltText)
          .HasMaxLength(250)
          .IsRequired();

      entity.HasIndex(image => new
      {
        image.ProductId,
        image.SortOrder
      });

      entity.HasOne(image => image.Product)
          .WithMany(product => product.Images)
          .HasForeignKey(image => image.ProductId)
          .OnDelete(DeleteBehavior.Cascade);
    }
  }
}