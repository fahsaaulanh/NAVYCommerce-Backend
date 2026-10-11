using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NAVYCommerce.Application.Abstractions.Storage;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.BackgroundServices;

public sealed class StorageDeletionWorker : BackgroundService
{
  private const int BatchSize = 5;

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<StorageDeletionWorker> _logger;

  public StorageDeletionWorker(
      IServiceScopeFactory scopeFactory,
      ILogger<StorageDeletionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(
      CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        for (var index = 0; index < BatchSize; index++)
        {
          stoppingToken.ThrowIfCancellationRequested();

          var foundJob = await ProcessOneAsync(
              stoppingToken);

          if (!foundJob)
          {
            break;
          }
        }
      }
      catch (OperationCanceledException)
          when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception exception)
      {
        _logger.LogError(
            exception,
            "Gagal memproses antrean penghapusan storage.");
      }

      try
      {
        await Task.Delay(
            TimeSpan.FromSeconds(10),
            stoppingToken);
      }
      catch (OperationCanceledException)
          when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
    }
  }

  private async Task<bool> ProcessOneAsync(
      CancellationToken stoppingToken)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();

    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var storage = scope.ServiceProvider
        .GetRequiredService<IProductImageStorage>();

    await using var transaction =
        await context.Database.BeginTransactionAsync(
            stoppingToken);

    var now = DateTime.UtcNow;

    var rows = await context.StorageDeletionJobs
        .FromSqlInterpolated($"""
                SELECT *
                FROM storage_deletion_jobs
                WHERE "NextAttemptAt" <= {now}
                ORDER BY "NextAttemptAt", "CreatedAt", "Id"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
        .ToListAsync(stoppingToken);

    var job = rows.SingleOrDefault();

    if (job == null)
    {
      return false;
    }

    try
    {
      using var timeout =
          CancellationTokenSource.CreateLinkedTokenSource(
              stoppingToken);

      timeout.CancelAfter(TimeSpan.FromSeconds(30));

      await storage.DeleteAsync(
          job.StorageKey,
          timeout.Token);
    }
    catch (OperationCanceledException)
        when (stoppingToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      if (job.Attempts < int.MaxValue)
      {
        job.Attempts++;
      }

      var exponent = Math.Min(job.Attempts, 10);

      var retrySeconds = Math.Min(
          3600,
          5 * Math.Pow(2, exponent));

      job.NextAttemptAt = DateTime.UtcNow
          .AddSeconds(retrySeconds);

      await context.SaveChangesAsync(stoppingToken);
      await transaction.CommitAsync(stoppingToken);

      _logger.LogWarning(
          exception,
          "Penghapusan storage untuk job {JobId} gagal. Percobaan {Attempts}; dijadwalkan ulang pada {NextAttemptAt}.",
          job.Id,
          job.Attempts,
          job.NextAttemptAt);

      return true;
    }

    context.StorageDeletionJobs.Remove(job);

    await context.SaveChangesAsync(stoppingToken);
    await transaction.CommitAsync(stoppingToken);

    _logger.LogInformation(
        "Tugas penghapusan storage {JobId} selesai.",
        job.Id);

    return true;
  }
}