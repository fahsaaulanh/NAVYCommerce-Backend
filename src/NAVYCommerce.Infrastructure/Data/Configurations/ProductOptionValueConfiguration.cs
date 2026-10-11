using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public class ProductOptionValueConfiguration
      : IEntityTypeConfiguration<ProductOptionValue>
  {
    public void Configure(EntityTypeBuilder<ProductOptionValue> entity)
    {
      entity.ToTable("product_option_values");

      entity.HasKey(value => value.Id);

      entity.HasAlternateKey(value => new
      {
        value.ProductId,
        value.OptionId,
        value.Id
      });

      entity.Property(value => value.Value)
          .HasMaxLength(50)
          .IsRequired();

      entity.HasIndex(value => new
      {
        value.OptionId,
        value.Value
      }).IsUnique();

      entity.HasOne(value => value.Option)
          .WithMany(option => option.Values)
          .HasForeignKey(value => new
          {
            value.ProductId,
            value.OptionId
          })
          .HasPrincipalKey(option => new
          {
            option.ProductId,
            option.Id
          })
          .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(value => value.Image)
          .WithMany()
          .HasForeignKey(value => new
          {
            value.ProductId,
            value.ImageId
          })
          .HasPrincipalKey(image => new
          {
            image.ProductId,
            image.Id
          })
          .OnDelete(DeleteBehavior.NoAction);
    }
  }
}