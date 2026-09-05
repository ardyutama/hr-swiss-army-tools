using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hr_sat.Application.Features.Candidates.Import;

internal sealed class ImportCandidatesCommandHandler(
    IApplicationDbContext dbContext,
    IPrivateFileStorage fileStorage,
    TimeProvider timeProvider,
    ILogger<ImportFilePreparer> filePreparerLogger)
    : ICommandHandler<ImportCandidatesCommand, ImportCandidatesResponse>
{
    public async Task<Result<ImportCandidatesResponse>> Handle(
        ImportCandidatesCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(
            command.VacancyId,
            cancellationToken);
        if (vacancy is null)
        {
            return Result<ImportCandidatesResponse>.Failure(
                CandidateErrors.NotFound(command.VacancyId));
        }

        var canReceiveResult = vacancy.EnsureCanReceiveCandidateImport();
        if (canReceiveResult.IsFailure)
        {
            return Result<ImportCandidatesResponse>.Failure(canReceiveResult.Error);
        }

        var existingHashKeys = (await dbContext.Candidates
                .Where(candidate => candidate.VacancyId == command.VacancyId)
                .Select(candidate => candidate.SourceSha256)
                .ToListAsync(cancellationToken))
            .Select(Convert.ToHexString)
            .ToHashSet(StringComparer.Ordinal);
        var filePreparer = new ImportFilePreparer(
            command.VacancyId,
            existingHashKeys,
            dbContext,
            fileStorage,
            timeProvider,
            filePreparerLogger);
        var files = command.Files!;
        var outcomes = new List<ImportFileOutcome>(files.Count);

        try
        {
            foreach (var file in files)
            {
                outcomes.Add(await filePreparer.PrepareAsync(file, cancellationToken));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await filePreparer.DeleteStoredFilesAsync();
            throw;
        }

        return new ImportCandidatesResponse(
            outcomes
                .Select(outcome => outcome.ToResponse(command.VacancyId))
                .ToList());
    }
}
