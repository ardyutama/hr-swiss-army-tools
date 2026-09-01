namespace hr_sat.Application.Abstractions.Storage;

public interface IPrivateFileStorage
{
    Task<StoredFile> StoreAsync(
        ReadOnlyMemory<byte> content,
        string category,
        string extension,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the storage keys of all stored files, oldest modification first.
    /// </summary>
    Task<IReadOnlyList<string>> ListStorageKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns true when the stored file predates <paramref name="threshold"/>,
    /// meaning it cannot still be attached to an in-flight write.
    /// </summary>
    Task<bool> IsOlderThanAsync(
        string storageKey,
        TimeSpan threshold,
        CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, long SizeBytes, byte[] Sha256);