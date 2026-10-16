using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public sealed class CartItemConfiguration
      : IEntityTypeConfiguration<CartItem>
  {
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
      builder.ToTable("cart_items", table =>
          table.HasCheckConstraint(
              "CK_cart_items_Quantity_Positive",
              "\"Quantity\" > 0"));

      builder.HasKey(item => item.Id);

      builder.HasIndex(item => new
          {
            item.CartId,
            item.ProductVariantId
          })
          .IsUnique()
          .HasDatabaseName(
              "IX_cart_items_CartId_ProductVariantId");

      builder.HasOne(item => item.Cart)
          .WithMany(cart => cart.Items)
          .HasForeignKey(item => item.CartId)
          .OnDelete(DeleteBehavior.Cascade);

      builder.HasOne(item => item.ProductVariant)
          .WithMany()
          .HasForeignKey(item => item.ProductVariantId)
          .OnDelete(DeleteBehavior.Cascade);
    }
  }
}
