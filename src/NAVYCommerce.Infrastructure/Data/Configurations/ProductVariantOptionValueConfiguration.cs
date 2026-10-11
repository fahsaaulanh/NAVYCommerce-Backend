using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public class ProductVariantOptionValueConfiguration
      : IEntityTypeConfiguration<ProductVariantOptionValue>
  {
    public void Configure(
        EntityTypeBuilder<ProductVariantOptionValue> entity)
    {
      entity.ToTable("product_variant_option_values");

      entity.HasKey(link => new
      {
        link.VariantId,
        link.OptionId
      });

      entity.HasOne(link => link.Variant)
          .WithMany(variant => variant.OptionValues)
          .HasForeignKey(link => new
          {
            link.ProductId,
            link.VariantId
          })
          .HasPrincipalKey(variant => new
          {
            variant.ProductId,
            variant.Id
          })
          .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(link => link.OptionValue)
          .WithMany()
          .HasForeignKey(link => new
          {
            link.ProductId,
            link.OptionId,
            link.OptionValueId
          })
          .HasPrincipalKey(value => new
          {
            value.ProductId,
            value.OptionId,
            value.Id
          })
          .OnDelete(DeleteBehavior.NoAction);
    }
  }
}