using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.Delete;

internal sealed class DeleteCandidateCommandHandler(
    IApplicationDbContext dbContext,
    IPrivateFileStorage fileStorage)
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

        await transaction.CommitAsync(cancellationToken);

        foreach (var storageKey in documentStorageKeys.Prepend(sourceStorageKey))
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }

        return Result.Success();
    }
}
