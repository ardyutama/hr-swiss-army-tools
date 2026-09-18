using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class PurgeVacancyCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<PurgeVacancyCommand>
{
    public async Task<Result> Handle(
        PurgeVacancyCommand command,
        CancellationToken cancellationToken)
    {
        return await VacancyWrite.ExecuteLockedAsync<object?>(
            command.Id,
            dbContext,
            async vacancy =>
            {
                var sourceStorageKeys = await dbContext.Candidates
                    .Where(candidate => dbContext.IntakeRounds.Any(round =>
                        round.Id == candidate.IntakeRoundId && round.VacancyId == vacancy.Id))
                    .Select(candidate => candidate.SourceStorageKey)
                    .Where(storageKey => storageKey != null)
                    .Select(storageKey => storageKey!)
                    .ToListAsync(cancellationToken);
                var documentStorageKeys = await dbContext.CvDocuments
                    .Where(document => dbContext.Candidates.Any(candidate =>
                        candidate.Id == document.CandidateId &&
                        dbContext.IntakeRounds.Any(round =>
                            round.Id == candidate.IntakeRoundId && round.VacancyId == vacancy.Id)))
                    .Select(document => document.StorageKey)
                    .ToListAsync(cancellationToken);
                await dbContext.Vacancies
                    .Where(item => item.Id == vacancy.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                var enqueuedAt = timeProvider.GetUtcNow();
                foreach (var storageKey in sourceStorageKeys.Concat(documentStorageKeys))
                {
                    dbContext.PendingFileDeletions.Add(new PendingFileDeletion
                    {
                        StorageKey = storageKey,
                        EnqueuedAt = enqueuedAt
                    });
                }

                return Result<object?>.Success(null);
            },
            cancellationToken);
    }
}
