using hr_sat.Application.Abstractions.Data;
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
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(
            command.VacancyId,
            cancellationToken);
        if (vacancy is null)
        {
            return CandidateErrors.NotFound(command.VacancyId);
        }

        var canRemoveResult = vacancy.EnsureCanRemoveCandidate();
        if (canRemoveResult.IsFailure)
        {
            return canRemoveResult.Error;
        }

        var sourceStorageKey = await dbContext.Candidates
            .Where(candidate =>
                candidate.Id == command.CandidateId &&
                candidate.VacancyId == command.VacancyId)
            .Select(candidate => candidate.SourceStorageKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceStorageKey is null)
        {
            return CandidateErrors.NotFound(command.CandidateId);
        }

        var documentStorageKeys = await dbContext.CvDocuments
            .Where(document => document.CandidateId == command.CandidateId)
            .Select(document => document.StorageKey)
            .ToListAsync(cancellationToken);

        await dbContext.Candidates
            .Where(candidate => candidate.Id == command.CandidateId)
            .ExecuteDeleteAsync(cancellationToken);

        var enqueuedAt = timeProvider.GetUtcNow();
        foreach (var storageKey in documentStorageKeys.Prepend(sourceStorageKey))
        {
            dbContext.PendingFileDeletions.Add(new PendingFileDeletion
            {
                StorageKey = storageKey,
                EnqueuedAt = enqueuedAt
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
