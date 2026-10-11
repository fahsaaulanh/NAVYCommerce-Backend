using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public class ProductOptionConfiguration
      : IEntityTypeConfiguration<ProductOption>
  {
    public void Configure(EntityTypeBuilder<ProductOption> entity)
    {
      entity.ToTable("product_options");

      entity.HasKey(option => option.Id);

      entity.HasAlternateKey(option => new
      {
        option.ProductId,
        option.Id
      });

      entity.Property(option => option.Name)
          .HasMaxLength(50)
          .IsRequired();

      entity.HasIndex(option => new
      {
        option.ProductId,
        option.Name
      }).IsUnique();

      entity.HasOne(option => option.Product)
          .WithMany(product => product.Options)
          .HasForeignKey(option => option.ProductId)
          .OnDelete(DeleteBehavior.Cascade);
    }
  }
}