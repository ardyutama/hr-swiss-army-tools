using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class PurgeVacancyCommandHandler(
    IApplicationDbContext dbContext,
    IPrivateFileStorage fileStorage)
    : ICommandHandler<PurgeVacancyCommand>
{
    public async Task<Result> Handle(
        PurgeVacancyCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(command.Id, cancellationToken);
        if (vacancy is null)
        {
            return VacancyErrors.NotFound(command.Id);
        }

        var sourceStorageKeys = await dbContext.Candidates
            .Where(candidate => candidate.VacancyId == command.Id)
            .Select(candidate => candidate.SourceStorageKey)
            .ToListAsync(cancellationToken);
        var documentStorageKeys = await dbContext.CvDocuments
            .Where(document => dbContext.Candidates.Any(candidate =>
                candidate.Id == document.CandidateId && candidate.VacancyId == command.Id))
            .Select(document => document.StorageKey)
            .ToListAsync(cancellationToken);
        var deletedCount = await dbContext.Vacancies
            .Where(vacancy => vacancy.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount == 0)
        {
            return VacancyErrors.NotFound(command.Id);
        }

        await transaction.CommitAsync(cancellationToken);

        foreach (var storageKey in sourceStorageKeys.Concat(documentStorageKeys))
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }

        return Result.Success();
    }
}
