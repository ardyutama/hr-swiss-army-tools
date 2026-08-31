using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using hr_sat.Server.Infrastructure.Vacancies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Candidates;

internal static class DeleteCandidate
{
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> HandleAsync(
        long vacancyId,
        long candidateId,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(vacancyId, cancellationToken);
        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        vacancy.EnsureCanRemoveCandidate();

        var sourceStorageKey = await dbContext.Candidates
            .Where(candidate => candidate.Id == candidateId && candidate.VacancyId == vacancyId)
            .Select(candidate => candidate.SourceStorageKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceStorageKey is null)
        {
            return TypedResults.NotFound();
        }

        var documentStorageKeys = await dbContext.CvDocuments
            .Where(document => document.CandidateId == candidateId)
            .Select(document => document.StorageKey)
            .ToListAsync(cancellationToken);

        await dbContext.Candidates
            .Where(candidate => candidate.Id == candidateId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var storageKey in documentStorageKeys.Prepend(sourceStorageKey))
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }

        return TypedResults.NoContent();
    }
}
