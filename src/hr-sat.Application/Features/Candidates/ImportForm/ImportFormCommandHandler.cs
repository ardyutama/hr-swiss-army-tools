using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;

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
            async (vacancy, round) => await ImportRowsAsync(
                command,
                vacancy,
                round,
                parsedCsv,
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

    private async Task<Result<ImportFormResponse>> ImportRowsAsync(
        ImportFormCommand command,
        Vacancy vacancy,
        IntakeRound round,
        ParsedFormCsv parsedCsv,
        CancellationToken cancellationToken)
    {
        FormLayout? layout;
        if (command.Layout is not null)
        {
            var layoutResult = await ApplyLayoutAsync(
                command.Layout,
                vacancy,
                parsedCsv.Headers,
                cancellationToken);
            if (layoutResult.IsFailure)
            {
                return Result<ImportFormResponse>.Failure(layoutResult.Error);
            }

            layout = layoutResult.Value;
        }
        else
        {
            layout = vacancy.FormLayout;
            if (layout is null || !layout.IsValid)
            {
                return Result<ImportFormResponse>.Failure(
                    CandidateErrors.FormLayoutRequired(
                        command.VacancyId,
                        parsedCsv.Headers));
            }

            var changes = layout.DetectDrift(parsedCsv.Headers);
            if (changes.Count > 0 && !command.ConfirmDrift)
            {
                return Result<ImportFormResponse>.Failure(
                    CandidateErrors.FormHeaderDrift(
                        parsedCsv.Headers,
                        changes));
            }

            if (command.ConfirmDrift)
            {
                var snapshotResult = layout.AdoptHeaderSnapshot(parsedCsv.Headers);
                if (snapshotResult.IsFailure)
                {
                    return Result<ImportFormResponse>.Failure(snapshotResult.Error);
                }
            }
        }

        var currentRoundResponses = await (
            from response in dbContext.CandidateFormResponses
            join candidate in dbContext.Candidates
                on response.CandidateId equals candidate.Id
            where candidate.IntakeRoundId == round.Id &&
                response.IsCurrent && response.IdentityKey != null
            select new CurrentFormResponse(response, candidate))
            .ToListAsync(cancellationToken);
        var currentResponsesByKey = currentRoundResponses
            .GroupBy(item => item.Response.IdentityKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var vacancyFormEmailIdentities = await (
            from response in dbContext.CandidateFormResponses.AsNoTracking()
            join candidate in dbContext.Candidates.AsNoTracking()
                on response.CandidateId equals candidate.Id
            join candidateRound in dbContext.IntakeRounds.AsNoTracking()
                on candidate.IntakeRoundId equals candidateRound.Id
            where candidateRound.VacancyId == command.VacancyId &&
                response.IsCurrent && response.IdentityKey != null
            select new
            {
                response.IdentityKey,
                CandidateId = candidate.Id
            })
            .ToListAsync(cancellationToken);
        var formEmailCandidateIdsByKey = vacancyFormEmailIdentities
            .Where(item => CandidateFormIdentity.IsEmailKey(item.IdentityKey))
            .GroupBy(item => item.IdentityKey!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.CandidateId).ToHashSet(),
                StringComparer.Ordinal);
        var emailCandidateKeys = (await (
                from candidate in dbContext.Candidates.AsNoTracking()
                join candidateRound in dbContext.IntakeRounds.AsNoTracking()
                    on candidate.IntakeRoundId equals candidateRound.Id
                where candidateRound.VacancyId == command.VacancyId &&
                    candidate.SourceSenderEmail != null
                select candidate.SourceSenderEmail)
            .ToListAsync(cancellationToken))
            .Select(CandidateFormIdentity.NormalizeEmail)
            .Where(email => email is not null)
            .Select(email => email!)
            .ToHashSet(StringComparer.Ordinal);

        var groupedRows = GroupRows(parsedCsv.Rows);
        var importedAt = timeProvider.GetUtcNow();
        var created = 0;
        var updated = 0;
        var skippedOutdated = 0;
        var priorApplications = 0;

        foreach (var rows in groupedRows)
        {
            var identityKey = rows[0].IdentityKey;
            if (identityKey is null)
            {
                var candidateResult = AddNewCandidate(
                    vacancy,
                    round,
                    rows,
                    importedAt,
                    layout,
                    isResubmitted: false);
                if (candidateResult.IsFailure)
                {
                    return Result<ImportFormResponse>.Failure(candidateResult.Error);
                }

                created++;
                continue;
            }

            if (!currentResponsesByKey.TryGetValue(identityKey, out var currentResponse))
            {
                var candidateResult = AddNewCandidate(
                    vacancy,
                    round,
                    rows,
                    importedAt,
                    layout,
                    isResubmitted: false);
                if (candidateResult.IsFailure)
                {
                    return Result<ImportFormResponse>.Failure(candidateResult.Error);
                }

                if (CandidateFormIdentity.IsEmailKey(identityKey) &&
                    HasPriorApplication(
                        identityKey,
                        null,
                        emailCandidateKeys,
                        formEmailCandidateIdsByKey))
                {
                    priorApplications++;
                }

                created++;
                continue;
            }

            var freshRows = rows
                .Where(row => IsLater(row, currentResponse.Response))
                .ToList();
            if (freshRows.Count == 0)
            {
                skippedOutdated += rows.Count;
                continue;
            }

            var winner = FindLatest(freshRows);
            var candidate = currentResponse.Candidate;
            var response = currentResponse.Response;
            foreach (var row in freshRows.Where(row => row.RowPosition != winner.RowPosition))
            {
                var addResult = candidate.AddFormResponse(
                    ToResponseData(row, importedAt),
                    response,
                    isResubmitted: true);
                if (addResult.IsFailure)
                {
                    return Result<ImportFormResponse>.Failure(addResult.Error);
                }

                response = addResult.Value;
            }

            var winnerResult = candidate.AddFormResponse(
                ToResponseData(winner, importedAt),
                response,
                isResubmitted: true);
            if (winnerResult.IsFailure)
            {
                return Result<ImportFormResponse>.Failure(winnerResult.Error);
            }

            candidate.PrefillDetailsFromLayout(layout);
            skippedOutdated += rows.Count - freshRows.Count;
            if (CandidateFormIdentity.IsEmailKey(identityKey) &&
                HasPriorApplication(
                    identityKey,
                    candidate.Id,
                    emailCandidateKeys,
                    formEmailCandidateIdsByKey))
            {
                priorApplications++;
            }

            updated++;
        }

        return new ImportFormResponse(
            parsedCsv.Rows.Count,
            created,
            updated,
            skippedOutdated,
            priorApplications);
    }

    private async Task<Result<FormLayout>> ApplyLayoutAsync(
        FormLayoutDefinition definition,
        Vacancy vacancy,
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken)
    {
        var hadLayout = vacancy.FormLayout is not null;
        var layoutResult = vacancy.UpsertFormLayout(definition, headers);
        if (layoutResult.IsFailure)
        {
            return layoutResult;
        }

        if (!hadLayout)
        {
            dbContext.FormLayouts.Add(layoutResult.Value);
        }

        await ReprojectActiveCandidatesAsync(
            vacancy,
            layoutResult.Value,
            cancellationToken);
        return layoutResult.Value;
    }

    private async Task ReprojectActiveCandidatesAsync(
        Vacancy vacancy,
        FormLayout layout,
        CancellationToken cancellationToken)
    {
        var activeRound = vacancy.ActiveRound;
        if (activeRound is null)
        {
            return;
        }

        var candidates = await dbContext.Candidates
            .Where(candidate =>
                candidate.IntakeRoundId == activeRound.Id &&
                candidate.IntakeSource == CandidateIntakeSource.Form)
            .Include(candidate => candidate.FormResponses)
            .ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            candidate.PrefillDetailsFromLayout(layout);
        }
    }

    private Result<Candidate> AddNewCandidate(
        Vacancy vacancy,
        IntakeRound round,
        IReadOnlyList<ParsedFormRow> rows,
        DateTimeOffset importedAt,
        FormLayout layout,
        bool isResubmitted)
    {
        var candidateResult = vacancy.ImportFormCandidate(new CandidateFormImportData(
            round.Id,
            importedAt));
        if (candidateResult.IsFailure)
        {
            return candidateResult;
        }

        var candidate = candidateResult.Value;
        var latest = FindLatest(rows);
        CandidateFormResponse? currentResponse = null;
        foreach (var row in rows.Where(row => row.RowPosition != latest.RowPosition))
        {
            var addResult = candidate.AddFormResponse(
                ToResponseData(row, importedAt),
                currentResponse,
                isResubmitted);
            if (addResult.IsFailure)
            {
                return Result<Candidate>.Failure(addResult.Error);
            }

            currentResponse = addResult.Value;
        }

        var latestResult = candidate.AddFormResponse(
            ToResponseData(latest, importedAt),
            currentResponse,
            isResubmitted);
        if (latestResult.IsFailure)
        {
            return Result<Candidate>.Failure(latestResult.Error);
        }

        candidate.PrefillDetailsFromLayout(layout);
        dbContext.Candidates.Add(candidate);
        return candidate;
    }

    private static IReadOnlyList<IReadOnlyList<ParsedFormRow>> GroupRows(
        IReadOnlyList<ParsedFormRow> rows)
    {
        var groupedRows = new List<IReadOnlyList<ParsedFormRow>>();
        var keyedRows = new Dictionary<string, List<ParsedFormRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.IdentityKey is null)
            {
                groupedRows.Add([row]);
                continue;
            }

            if (!keyedRows.TryGetValue(row.IdentityKey, out var group))
            {
                group = [];
                keyedRows.Add(row.IdentityKey, group);
                groupedRows.Add(group);
            }

            group.Add(row);
        }

        return groupedRows;
    }

    private static ParsedFormRow FindLatest(IReadOnlyList<ParsedFormRow> rows)
    {
        var latest = rows[0];
        foreach (var row in rows.Skip(1))
        {
            if (IsLater(row, latest))
            {
                latest = row;
            }
        }

        return latest;
    }

    private static bool IsLater(ParsedFormRow incoming, CandidateFormResponse current) =>
        IsLater(incoming.FormTimestampParsed, current.FormTimestampParsed);

    private static bool IsLater(
        DateTimeOffset? incoming,
        DateTimeOffset? current)
    {
        if (incoming.HasValue && current.HasValue)
        {
            return incoming.Value >= current.Value;
        }

        return true;
    }

    private static bool IsLater(ParsedFormRow incoming, ParsedFormRow current)
    {
        if (incoming.FormTimestampParsed.HasValue && current.FormTimestampParsed.HasValue)
        {
            return incoming.FormTimestampParsed.Value >= current.FormTimestampParsed.Value;
        }

        return true;
    }

    private static CandidateFormResponseData ToResponseData(
        ParsedFormRow row,
        DateTimeOffset importedAt) => new(
        row.Cells,
        row.FormTimestampRaw,
        row.FormTimestampParsed,
        row.IdentityKey,
        importedAt);

    private static bool HasPriorApplication(
        string identityKey,
        long? currentCandidateId,
        IReadOnlySet<string> emailCandidateKeys,
        IReadOnlyDictionary<string, HashSet<long>> formEmailCandidateIdsByKey) =>
        emailCandidateKeys.Contains(identityKey) ||
        (formEmailCandidateIdsByKey.TryGetValue(identityKey, out var candidateIds) &&
            candidateIds.Any(candidateId => candidateId != currentCandidateId));

    private sealed record CurrentFormResponse(
        CandidateFormResponse Response,
        Candidate Candidate);
}
