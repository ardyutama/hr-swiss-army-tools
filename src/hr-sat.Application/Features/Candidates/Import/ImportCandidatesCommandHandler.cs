using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Shared;
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
        ImportFilePreparer? filePreparer = null;
        try
        {
            var writeResult = await RoundWrite.ExecuteAsync<IReadOnlyList<ImportFileOutcome>>(
                command.VacancyId,
                command.RoundId,
                dbContext,
                (vacancy, targetRoundId) => vacancy.EnsureCanReceiveCandidateImport(targetRoundId),
                async (_, round) =>
                {
                    var existingHashKeys = (await dbContext.Candidates
                            .Where(candidate => candidate.IntakeRoundId == round.Id)
                            .Select(candidate => candidate.SourceSha256)
                            .ToListAsync(cancellationToken))
                        .Select(Convert.ToHexString)
                        .ToHashSet(StringComparer.Ordinal);
                    filePreparer = new ImportFilePreparer(
                        round.Id,
                        existingHashKeys,
                        dbContext,
                        fileStorage,
                        timeProvider,
                        filePreparerLogger);
                    var files = command.Files!;
                    var outcomes = new List<ImportFileOutcome>(files.Count);

                    foreach (var file in files)
                    {
                        outcomes.Add(await filePreparer.PrepareAsync(file, cancellationToken));
                    }

                    return Result<IReadOnlyList<ImportFileOutcome>>.Success(outcomes);
                },
                cancellationToken);
            if (writeResult.IsFailure)
            {
                return Result<ImportCandidatesResponse>.Failure(writeResult.Error);
            }

            return new ImportCandidatesResponse(
                writeResult.Value
                    .Select(outcome => outcome.ToResponse(command.VacancyId, command.RoundId))
                    .ToList());
        }
        catch
        {
            if (filePreparer is not null)
            {
                await filePreparer.DeleteStoredFilesAsync();
            }

            throw;
        }
    }
}
