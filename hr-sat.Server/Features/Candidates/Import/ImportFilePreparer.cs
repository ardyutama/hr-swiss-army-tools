using System.Security.Cryptography;
using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;

namespace hr_sat.Server.Features.Candidates.Import;

internal sealed class ImportFilePreparer(
    long vacancyId,
    IReadOnlySet<string> existingHashKeys,
    AppDbContext dbContext,
    IPrivateFileStorage fileStorage,
    TimeProvider timeProvider)
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024;
    private static readonly string[] AcceptedContentTypes =
    [
        "application/octet-stream",
        "application/eml",
        "application/x-eml",
        "message/rfc822",
        "text/plain"
    ];

    private readonly HashSet<string> batchHashKeys = new(StringComparer.Ordinal);
    private readonly List<string> storedKeys = [];

    public async Task<ImportFileOutcome> PrepareAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var originalFilename = GetSafeFilename(file.FileName);
        if (!originalFilename.EndsWith(".eml", StringComparison.OrdinalIgnoreCase))
        {
            return ImportFileOutcome.Failed(
                originalFilename,
                "Only files with the .eml extension can be imported.");
        }

        if (file.Length <= 0)
        {
            return ImportFileOutcome.Failed(originalFilename, "The .eml file must not be empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return ImportFileOutcome.Failed(
                originalFilename,
                "The .eml file must be 25 MB or smaller.");
        }

        if (!IsAcceptedContentType(file.ContentType))
        {
            return ImportFileOutcome.Failed(
                originalFilename,
                "The uploaded file has an unsupported content type.");
        }

        byte[] sourceBytes;
        try
        {
            await using var sourceStream = file.OpenReadStream();
            await using var memoryStream = new MemoryStream(checked((int)file.Length));
            await sourceStream.CopyToAsync(memoryStream, cancellationToken);
            sourceBytes = memoryStream.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return ImportFileOutcome.Failed(originalFilename, "The .eml file could not be read.");
        }

        var sourceHash = SHA256.HashData(sourceBytes);
        var sourceHashKey = Convert.ToHexString(sourceHash);
        if (existingHashKeys.Contains(sourceHashKey) || batchHashKeys.Contains(sourceHashKey))
        {
            return ImportFileOutcome.Skipped(
                originalFilename,
                "This .eml file was already imported into this vacancy.");
        }

        ParsedEml parsedEmail;
        try
        {
            parsedEmail = EmlParser.Parse(sourceBytes, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ImportFileOutcome.Failed(
                originalFilename,
                "The .eml file could not be parsed.");
        }

        if (parsedEmail.PdfAttachments.Count == 0)
        {
            return ImportFileOutcome.Failed(
                originalFilename,
                "The .eml file must contain at least one valid PDF attachment.");
        }

        var fileStorageKeys = new List<string>();
        try
        {
            var storedSource = await fileStorage.StoreAsync(
                sourceBytes,
                "source-emails",
                ".eml",
                cancellationToken);
            fileStorageKeys.Add(storedSource.StorageKey);
            storedKeys.Add(storedSource.StorageKey);

            var storedDocuments = new List<StoredCvDocument>(parsedEmail.PdfAttachments.Count);
            for (var index = 0; index < parsedEmail.PdfAttachments.Count; index++)
            {
                var attachment = parsedEmail.PdfAttachments[index];
                var storedDocument = await fileStorage.StoreAsync(
                    attachment.Content,
                    "cv-documents",
                    ".pdf",
                    cancellationToken);
                fileStorageKeys.Add(storedDocument.StorageKey);
                storedKeys.Add(storedDocument.StorageKey);
                storedDocuments.Add(new StoredCvDocument(
                    attachment.OriginalFilename,
                    storedDocument.StorageKey,
                    index + 1,
                    parsedEmail.PdfAttachments.Count == 1,
                    storedDocument.SizeBytes,
                    storedDocument.Sha256));
            }

            var candidate = Candidate.Import(
                vacancyId,
                parsedEmail.SenderName,
                parsedEmail.SenderEmail,
                parsedEmail.Subject,
                parsedEmail.BodyText,
                parsedEmail.SentAt,
                originalFilename,
                storedSource.StorageKey,
                storedSource.SizeBytes,
                sourceHash,
                timeProvider.GetUtcNow(),
                storedDocuments);
            dbContext.Candidates.Add(candidate);
            batchHashKeys.Add(sourceHashKey);
            return ImportFileOutcome.Imported(originalFilename, candidate);
        }
        catch (OperationCanceledException)
        {
            await DeleteStoredFilesAsync(fileStorage, fileStorageKeys);
            throw;
        }
        catch (CandidateValidationException)
        {
            await DeleteStoredFilesAsync(fileStorage, fileStorageKeys);
            return ImportFileOutcome.Failed(originalFilename, "The imported email is invalid.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await DeleteStoredFilesAsync(fileStorage, fileStorageKeys);
            return ImportFileOutcome.Failed(originalFilename, "The imported files could not be stored.");
        }
    }

    public Task DeleteStoredFilesAsync() => DeleteStoredFilesAsync(fileStorage, storedKeys);

    private static bool IsAcceptedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return AcceptedContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSafeFilename(string? filename)
    {
        var normalizedFilename = (filename ?? string.Empty).Replace('\\', '/');
        return Path.GetFileName(normalizedFilename);
    }

    private static async Task DeleteStoredFilesAsync(
        IPrivateFileStorage fileStorage,
        IEnumerable<string> storageKeys)
    {
        foreach (var storageKey in storageKeys.Distinct(StringComparer.Ordinal))
        {
            try
            {
                await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

internal sealed record ImportFileOutcome(
    string FileName,
    string Status,
    string? Error,
    Candidate? Candidate)
{
    public static ImportFileOutcome Imported(string fileName, Candidate candidate) =>
        new(fileName, "imported", null, candidate);

    public static ImportFileOutcome Skipped(string fileName, string error) =>
        new(fileName, "skipped", error, null);

    public static ImportFileOutcome Failed(string fileName, string error) =>
        new(fileName, "failed", error, null);

    public ImportFileResponse ToResponse(long vacancyId) => new(
        FileName,
        Status,
        Error,
        Candidate is null ? null : CandidateImportResponse.From(vacancyId, Candidate));
}