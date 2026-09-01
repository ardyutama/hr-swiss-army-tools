using System.Security.Cryptography;
using hr_sat.Application.Abstractions.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace hr_sat.Infrastructure.Storage;

public sealed class PrivateFileStorage : IPrivateFileStorage
{
    private const int BufferSize = 64 * 1024;
    private readonly string _rootPath;

    public PrivateFileStorage(
        IOptions<PrivateFileStorageOptions> options,
        IHostEnvironment environment)
    {
        var configuredPath = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? "private-files"
            : options.Value.RootPath;
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public async Task<StoredFile> StoreAsync(
        ReadOnlyMemory<byte> content,
        string category,
        string extension,
        CancellationToken cancellationToken)
    {
        if (content.Length == 0)
        {
            throw new ArgumentException("File content must not be empty.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(category) ||
            category.Contains('/') ||
            category.Contains('\\'))
        {
            throw new ArgumentException("The storage category is invalid.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(extension) ||
            !extension.StartsWith('.') ||
            extension.Contains('/') ||
            extension.Contains('\\'))
        {
            throw new ArgumentException("The storage extension is invalid.", nameof(extension));
        }

        var storageKey = $"{category}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = GetSafePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        try
        {
            await using var fileStream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await fileStream.WriteAsync(content, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            return new StoredFile(
                storageKey,
                content.Length,
                SHA256.HashData(content.Span));
        }
        catch
        {
            TryDelete(filePath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = GetSafePath(storageKey);
        Stream fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(fileStream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(GetSafePath(storageKey));
        return Task.CompletedTask;
    }

    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathRooted(storageKey))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(storageKey));
        }

        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(storageKey));
        }

        return fullPath;
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}