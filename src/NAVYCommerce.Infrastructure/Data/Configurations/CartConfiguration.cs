using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations
{
  public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
  {
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
      builder.ToTable("carts");

      builder.HasKey(cart => cart.Id);

      builder.HasIndex(cart => cart.CustomerId)
          .IsUnique()
          .HasDatabaseName("IX_carts_CustomerId");

      builder.HasOne(cart => cart.Customer)
          .WithOne(user => user.Cart)
          .HasForeignKey<Cart>(cart => cart.CustomerId)
          .OnDelete(DeleteBehavior.Cascade);
    }
  }
}
