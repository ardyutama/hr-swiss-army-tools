namespace hr_sat.Server.Infrastructure.Storage;

public interface IPrivateFileStorage
{
    Task<StoredFile> StoreAsync(
        ReadOnlyMemory<byte> content,
        string category,
        string extension,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, long SizeBytes, byte[] Sha256);