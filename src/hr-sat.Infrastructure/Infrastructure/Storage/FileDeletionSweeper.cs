using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hr_sat.Infrastructure.Storage;

/// <summary>
/// Periodically converges private file storage with the database: processes
/// pending file deletions written by delete/purge transactions, then sweeps
/// stored files that no database row and no pending deletion references
/// (crash-orphaned import files). Never throws; failures are logged and
/// retried on the next pass.
/// </summary>
public sealed class FileDeletionSweeper(
    IServiceProvider services,
    IPrivateFileStorage fileStorage,
    TimeProvider timeProvider,
    ILogger<FileDeletionSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OrphanThreshold = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);
        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        await ProcessPendingDeletionsAsync(cancellationToken);
        await SweepOrphanedFilesAsync(cancellationToken);
    }

    private async Task ProcessPendingDeletionsAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await dbContext.PendingFileDeletions
            .OrderBy(deletion => deletion.Id)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var deletion in pending)
        {
            try
            {
                await fileStorage.DeleteAsync(deletion.StorageKey, cancellationToken);
                dbContext.PendingFileDeletions.Remove(deletion);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to delete stored file {StorageKey}; will retry on the next sweep.",
                    deletion.StorageKey);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SweepOrphanedFilesAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var referencedKeys = (await dbContext.Candidates
                .Select(candidate => candidate.SourceStorageKey)
                .ToListAsync(cancellationToken))
            .Concat(await dbContext.CvDocuments
                .Select(document => document.StorageKey)
                .ToListAsync(cancellationToken))
            .Concat(await dbContext.PendingFileDeletions
                .Select(deletion => deletion.StorageKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var storedKeys = await fileStorage.ListStorageKeysAsync(cancellationToken);
        foreach (var storageKey in storedKeys)
        {
            if (referencedKeys.Contains(storageKey))
            {
                continue;
            }

            if (!await fileStorage.IsOlderThanAsync(storageKey, OrphanThreshold, cancellationToken))
            {
                continue;
            }

            try
            {
                await fileStorage.DeleteAsync(storageKey, cancellationToken);
                logger.LogWarning("Swept orphaned stored file {StorageKey}.", storageKey);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to sweep orphaned stored file {StorageKey}; will retry on the next sweep.",
                    storageKey);
            }
        }
    }
}
