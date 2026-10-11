namespace NAVYCommerce.Infrastructure.Data.Entities;

public sealed class StorageDeletionJob
{
  public Guid Id { get; set; } = Guid.NewGuid();

  public string StorageKey { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

  public int Attempts { get; set; }
}