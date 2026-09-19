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
        var deleteResult = await RoundWrite.ExecuteCandidateAsync<bool>(
            command.VacancyId,
            command.RoundId,
            command.CandidateId,
            dbContext,
            async (vacancy, candidateId) =>
            {
                var candidate = dbContext.Candidates.Local
                    .SingleOrDefault(item =>
                        item.Id == candidateId &&
                        item.IntakeRoundId == command.RoundId);
                if (candidate is null)
                {
                    return Result<bool>.Failure(CandidateErrors.NotFound(candidateId));
                }

                var canRemoveResult = vacancy.EnsureCanRemoveCandidate(
                    command.RoundId,
                    candidate);
                if (canRemoveResult.IsFailure)
                {
                    return Result<bool>.Failure(canRemoveResult.Error);
                }

                await dbContext.Candidates
                    .Where(item => item.Id == candidate.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                var enqueuedAt = timeProvider.GetUtcNow();
                var storageKeys = candidate.CvDocuments
                    .Select(document => document.StorageKey)
                    .AsEnumerable();
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
