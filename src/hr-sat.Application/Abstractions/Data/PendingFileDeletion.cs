namespace hr_sat.Application.Abstractions.Data;

/// <summary>
/// Operational record of a file that must be removed from private storage.
/// Written inside the same database transaction as the removal of the owning
/// row; processed asynchronously by the file-deletion sweeper.
/// </summary>
public sealed class PendingFileDeletion
{
    public long Id { get; set; }

    public required string StorageKey { get; set; }

    public DateTimeOffset EnqueuedAt { get; set; }
}
