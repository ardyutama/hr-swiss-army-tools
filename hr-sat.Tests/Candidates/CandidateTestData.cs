using System.Security.Cryptography;
using hr_sat.Application.Abstractions.Extraction;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Application.Features.Candidates.Import;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace hr_sat.Tests.Candidates;

internal static class CandidateTestData
{
    public static Vacancy CreateVacancy(bool closed = false)
    {
        var result = Vacancy.Create(
            "Data Analyst",
            new DateOnly(2026, 8, 20),
            ["SQL"]);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        if (closed)
        {
            result.Value.Close(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero));
        }

        return result.Value;
    }

    public static Candidate CreateCandidate(
        long vacancyId,
        int sourceNumber = 1,
        DateTimeOffset? importedAt = null)
    {
        var sourceHash = new byte[32];
        sourceHash[0] = (byte)sourceNumber;
        var candidateResult = Candidate.Import(
            vacancyId,
            $"Candidate {sourceNumber}",
            $"candidate{sourceNumber}@example.com",
            $"Candidate {sourceNumber} application",
            "Please find my CV attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            $"candidate{sourceNumber}.eml",
            $"source-emails/candidate{sourceNumber}.eml",
            100,
            sourceHash,
            importedAt ?? new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            [new StoredCvDocument(
                $"candidate{sourceNumber}.pdf",
                $"cv-documents/candidate{sourceNumber}.pdf",
                1,
                true,
                20,
                sourceHash)]);
        if (candidateResult.IsFailure)
        {
            throw new InvalidOperationException(candidateResult.Error.Message);
        }

        return candidateResult.Value;
    }

    public static async Task<(Vacancy Vacancy, Candidate Candidate)> SeedCandidateAsync(
        TestDbContext dbContext,
        bool closed = false)
    {
        var vacancy = CreateVacancy(closed);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var candidate = CreateCandidate(vacancy.Id);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (vacancy, candidate);
    }

    public static CandidateCvExtractionService CreateExtractionService(string text = "") =>
        new(
            new TestPdfTextExtractor(text),
            NullLogger<CandidateCvExtractionService>.Instance);
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class TestFileStorage : IPrivateFileStorage
{
    private readonly Dictionary<string, byte[]> files = [];
    private readonly List<string> deletedKeys = [];
    private int nextStorageId;

    public IReadOnlyDictionary<string, byte[]> Files => files;
    public IReadOnlyList<string> DeletedKeys => deletedKeys;

    public void Add(string storageKey, byte[] content) => files[storageKey] = content.ToArray();

    public Task<StoredFile> StoreAsync(
        ReadOnlyMemory<byte> content,
        string category,
        string extension,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = content.ToArray();
        var storageKey = $"{category}/test-{++nextStorageId}{extension}";
        files[storageKey] = bytes;
        return Task.FromResult(new StoredFile(storageKey, bytes.Length, SHA256.HashData(bytes)));
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return files.TryGetValue(storageKey, out var content)
            ? Task.FromResult<Stream>(new MemoryStream(content, writable: false))
            : Task.FromException<Stream>(new FileNotFoundException(storageKey));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        deletedKeys.Add(storageKey);
        files.Remove(storageKey);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListStorageKeysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(files.Keys.ToArray());
    }

    public Task<bool> IsOlderThanAsync(
        string storageKey,
        TimeSpan threshold,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}

internal sealed class TestPdfTextExtractor(string text) : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(text);
    }
}