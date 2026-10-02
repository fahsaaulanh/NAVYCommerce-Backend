using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data.Configurations;


namespace NAVYCommerce.Infrastructure.Data
{
  public class ApplicationDbContext : DbContext
  {
    public ApplicationDbContext(DbContextOptions options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<UserOtp> UserOtps => Set<UserOtp>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Store> Stores => Set<Store>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
      modelBuilder.ApplyConfiguration(new StoreConfiguration());

      modelBuilder.Entity<User>().ToTable("users");
      modelBuilder.Entity<UserOtp>().ToTable("user_otps");
      modelBuilder.Entity<Category>().ToTable("categories");
      modelBuilder.Entity<Product>().ToTable("products");
      modelBuilder.Entity<ProductVariant>().ToTable("product_variants");

      modelBuilder.Entity<User>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.Role)
        .HasConversion<string>();
      });

      modelBuilder.Entity<UserOtp>(entity =>
        {
          entity.HasKey(e => e.Id);
          entity.Property(e => e.Type).HasConversion<string>();

          entity.HasOne(o => o.User)
                .WithMany(u => u.Otps)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

          entity.Property(otp => otp.FailedAttempts)
                .HasDefaultValue(0);
        });

      modelBuilder.Entity<Category>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Slug).IsUnique();
      });

      modelBuilder.Entity<Product>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Slug).IsUnique();
        entity.Property(e => e.VariantOptions)
        .HasColumnType("jsonb");

        entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

      });

      modelBuilder.Entity<ProductVariant>(entity =>
            {
              entity.HasKey(e => e.Id);
              entity.HasIndex(e => e.Sku).IsUnique();
              entity.Property(e => e.VariantAttributes).HasColumnType("jsonb");

              entity.HasOne(v => v.Product)
                    .WithMany(p => p.Variants)
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
    }

  }
}