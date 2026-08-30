using System.Security.Cryptography;
using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using hr_sat.Server.Infrastructure.Vacancies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Candidates;

internal static class ImportCandidates
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

    public static async Task<Results<Ok<ImportCandidatesResponse>, NotFound, ValidationProblem>> HandleAsync(
        long id,
        HttpRequest request,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        IFormFileCollection files;
        try
        {
            files = (await request.ReadFormAsync(cancellationToken)).Files;
        }
        catch (BadHttpRequestException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["The uploaded form data is invalid."]
            });
        }
        catch (InvalidDataException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["The uploaded form data is invalid."]
            });
        }

        if (files.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["At least one .eml file is required."]
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(id, cancellationToken);
        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            vacancy.EnsureCanReceiveCandidateImport();
        }
        catch (VacancyValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors);
        }

        var existingHashKeys = (await dbContext.Candidates
                .Where(candidate => candidate.VacancyId == id)
                .Select(candidate => candidate.SourceSha256)
                .ToListAsync(cancellationToken))
            .Select(Convert.ToHexString)
            .ToHashSet(StringComparer.Ordinal);
        var batchHashKeys = new HashSet<string>(StringComparer.Ordinal);
        var storedKeys = new List<string>();
        var outcomes = new List<ImportFileOutcome>(files.Count);

        try
        {
            foreach (var file in files)
            {
                outcomes.Add(await PrepareFileAsync(
                    id,
                    file,
                    existingHashKeys,
                    batchHashKeys,
                    dbContext,
                    fileStorage,
                    timeProvider,
                    storedKeys,
                    cancellationToken));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await DeleteStoredFilesAsync(fileStorage, storedKeys);
            throw;
        }

        return TypedResults.Ok(new ImportCandidatesResponse(
            outcomes
                .Select(outcome => outcome.ToResponse(id))
                .ToList()));
    }

    private static async Task<ImportFileOutcome> PrepareFileAsync(
        long vacancyId,
        IFormFile file,
        ISet<string> existingHashKeys,
        ISet<string> batchHashKeys,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        ICollection<string> storedKeys,
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

    private sealed record ImportFileOutcome(
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
}