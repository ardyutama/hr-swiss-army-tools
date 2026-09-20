using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Candidates.ImportForm;

internal sealed class ImportFormCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<ImportFormCommand, ImportFormResponse>
{
    public async Task<Result<ImportFormResponse>> Handle(
        ImportFormCommand command,
        CancellationToken cancellationToken)
    {
        ParsedFormCsv parsedCsv;
        try
        {
            parsedCsv = await FormCsvParser.ParseAsync(
                command.File!.Content,
                cancellationToken);
        }
        catch (FormCsvParseException exception)
        {
            return Result<ImportFormResponse>.Failure(
                CandidateErrors.InvalidFormCsv(exception.Message));
        }
        catch (IOException)
        {
            return Result<ImportFormResponse>.Failure(
                CandidateErrors.InvalidFormCsv("The CSV could not be read."));
        }

        var writeResult = await RoundWrite.ExecuteAsync(
            command.VacancyId,
            command.RoundId,
            dbContext,
            EnsureCanReceiveFormImport,
            async (vacancy, round) => await FormImportPipeline.ExecuteAsync(
                command,
                vacancy,
                round,
                parsedCsv,
                dbContext,
                timeProvider,
                cancellationToken),
            cancellationToken);

        return writeResult.IsFailure
            ? Result<ImportFormResponse>.Failure(writeResult.Error)
            : writeResult.Value;
    }

    private static Result<IntakeRound> EnsureCanReceiveFormImport(
        Vacancy vacancy,
        long roundId)
    {
        var result = vacancy.EnsureCanReceiveCandidateImport(roundId);
        if (result.IsFailure && result.Error is ValidationError validationError &&
            validationError.Errors.ContainsKey("status"))
        {
            return Result<IntakeRound>.Failure(Error.Conflict(
                "Candidates.FormImportLifecycleConflict",
                result.Error.Message));
        }

        return result;
    }
}
