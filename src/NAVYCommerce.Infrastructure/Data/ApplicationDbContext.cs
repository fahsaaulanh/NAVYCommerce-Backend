using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data.Configurations;
using NAVYCommerce.Infrastructure.Data.Entities;

namespace NAVYCommerce.Infrastructure.Data
{
  public class ApplicationDbContext : DbContext
  {
    public ApplicationDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserOtp> UserOtps => Set<UserOtp>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants
        => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages
        => Set<ProductImage>();

    public DbSet<ProductOption> ProductOptions
        => Set<ProductOption>();

    public DbSet<ProductOptionValue> ProductOptionValues
        => Set<ProductOptionValue>();

    public DbSet<ProductVariantOptionValue> ProductVariantOptionValues
        => Set<ProductVariantOptionValue>();

    public DbSet<StorageDeletionJob> StorageDeletionJobs
    => Set<StorageDeletionJob>();

    public DbSet<CustomerAddress> CustomerAddresses
    => Set<CustomerAddress>();

    public DbSet<StoreShippingOrigin> StoreShippingOrigins
    => Set<StoreShippingOrigin>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.ApplyConfiguration(new CartConfiguration());
      modelBuilder.ApplyConfiguration(new CartItemConfiguration());
      modelBuilder.ApplyConfiguration(
          new StorageDeletionJobConfiguration());
      modelBuilder.ApplyConfiguration(new StoreConfiguration());
      modelBuilder.ApplyConfiguration(new ProductImageConfiguration());
      modelBuilder.ApplyConfiguration(new ProductOptionConfiguration());
      modelBuilder.ApplyConfiguration(new ProductOptionValueConfiguration());
      modelBuilder.ApplyConfiguration(
          new ProductVariantOptionValueConfiguration());
      modelBuilder.ApplyConfiguration(
          new CustomerAddressConfiguration());

      modelBuilder.ApplyConfiguration(new StoreShippingOriginConfiguration());

      modelBuilder.ApplyConfiguration(
        new ShippingLocationMappingConfiguration());


      modelBuilder.Entity<User>(entity =>
      {
        entity.ToTable("users", table =>
              {
                table.HasCheckConstraint(
                          "CK_users_Email_Normalized",
                          "\"Email\" = lower(btrim(\"Email\"))");
              });

        entity.HasKey(user => user.Id);
        entity.HasIndex(user => user.Email).IsUnique();

        entity.Property(user => user.Role)
                  .HasConversion<string>();
      });

      modelBuilder.Entity<UserOtp>(entity =>
      {
        entity.ToTable("user_otps");

        entity.HasKey(otp => otp.Id);

        entity.Property(otp => otp.Type)
                  .HasConversion<string>();

        entity.Property(otp => otp.FailedAttempts)
                  .HasDefaultValue(0);

        entity.HasOne(otp => otp.User)
                  .WithMany(user => user.Otps)
                  .HasForeignKey(otp => otp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
      });

      modelBuilder.Entity<Category>(entity =>
      {
        entity.ToTable("categories");

        entity.HasKey(category => category.Id);
        entity.HasIndex(category => category.Slug).IsUnique();
      });

      modelBuilder.Entity<Product>(entity =>
      {
        entity.ToTable("products");

        entity.HasKey(product => product.Id);
        entity.HasIndex(product => product.Slug).IsUnique();

        entity.Property(product => product.VariantOptions)
                  .HasColumnType("jsonb");

        entity.HasOne(product => product.Category)
                  .WithMany(category => category.Products)
                  .HasForeignKey(product => product.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
      });

      modelBuilder.Entity<ProductVariant>(entity =>
      {
        entity.ToTable("product_variants");

        entity.HasKey(variant => variant.Id);

        entity.HasAlternateKey(variant => new
        {
          variant.ProductId,
          variant.Id
        });

        entity.HasIndex(variant => variant.Sku).IsUnique();

        entity.Property(variant => variant.VariantAttributes)
                  .HasColumnType("jsonb");

        entity.HasOne(variant => variant.Product)
                  .WithMany(product => product.Variants)
                  .HasForeignKey(variant => variant.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
      });
    }
  }
}