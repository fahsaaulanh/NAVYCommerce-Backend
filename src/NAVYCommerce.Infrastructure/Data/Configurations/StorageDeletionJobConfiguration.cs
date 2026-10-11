using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NAVYCommerce.Infrastructure.Data.Entities;

namespace NAVYCommerce.Infrastructure.Data.Configurations;

public sealed class StorageDeletionJobConfiguration
    : IEntityTypeConfiguration<StorageDeletionJob>
{
  public void Configure(
      EntityTypeBuilder<StorageDeletionJob> builder)
  {
    builder.ToTable("storage_deletion_jobs");

    builder.HasKey(job => job.Id);

    builder.Property(job => job.StorageKey)
        .HasMaxLength(500)
        .IsRequired();

    builder.Property(job => job.Attempts)
        .HasDefaultValue(0);

    builder.HasIndex(job => job.StorageKey)
        .IsUnique();

    builder.HasIndex(job => new
    {
      job.NextAttemptAt,
      job.CreatedAt
    });
  }
}