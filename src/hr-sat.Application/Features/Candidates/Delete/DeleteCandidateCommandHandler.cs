using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Shared;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.Delete;

internal sealed class DeleteCandidateCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteCandidateCommand>
{
    public async Task<Result> Handle(
        DeleteCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var deleteResult = await RoundWrite.ExecuteAsync(
            command.VacancyId,
            command.RoundId,
            dbContext,
            (vacancy, targetRoundId) => vacancy.EnsureCanRemoveCandidate(targetRoundId),
            async (_, _) =>
            {
                var candidate = await dbContext.Candidates
                    .Where(candidate =>
                        candidate.Id == command.CandidateId &&
                        candidate.IntakeRoundId == command.RoundId)
                    .Select(candidate => new
                    {
                        candidate.Id,
                        candidate.SourceStorageKey
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                if (candidate is null)
                {
                    return Result<bool>.Failure(CandidateErrors.NotFound(command.CandidateId));
                }

                var documentStorageKeys = await dbContext.CvDocuments
                    .Where(document => document.CandidateId == command.CandidateId)
                    .Select(document => document.StorageKey)
                    .ToListAsync(cancellationToken);

                await dbContext.Candidates
                    .Where(candidate => candidate.Id == command.CandidateId)
                    .ExecuteDeleteAsync(cancellationToken);

                var enqueuedAt = timeProvider.GetUtcNow();
                var storageKeys = documentStorageKeys.AsEnumerable();
                if (candidate.SourceStorageKey is not null)
                {
                    storageKeys = storageKeys.Prepend(candidate.SourceStorageKey);
                }

                foreach (var storageKey in storageKeys)
                {
                    dbContext.PendingFileDeletions.Add(new PendingFileDeletion
                    {
                        StorageKey = storageKey,
                        EnqueuedAt = enqueuedAt
                    });
                }

                return Result<bool>.Success(true);
            },
            cancellationToken);
        if (deleteResult.IsFailure)
        {
            return deleteResult.Error;
        }

        return Result.Success();
    }
}
