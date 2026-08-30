using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using hr_sat.Server.Infrastructure.Vacancies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class PurgeVacancy
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        long id,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(id, cancellationToken);
        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        var sourceStorageKeys = await dbContext.Candidates
            .Where(candidate => candidate.VacancyId == id)
            .Select(candidate => candidate.SourceStorageKey)
            .ToListAsync(cancellationToken);
        var documentStorageKeys = await dbContext.CvDocuments
            .Where(document => dbContext.Candidates.Any(candidate =>
                candidate.Id == document.CandidateId && candidate.VacancyId == id))
            .Select(document => document.StorageKey)
            .ToListAsync(cancellationToken);
        var deletedCount = await dbContext.Vacancies
            .Where(vacancy => vacancy.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount == 0)
        {
            return TypedResults.NotFound();
        }

        await transaction.CommitAsync(cancellationToken);

        foreach (var storageKey in sourceStorageKeys.Concat(documentStorageKeys))
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }

        return TypedResults.NoContent();
    }
}