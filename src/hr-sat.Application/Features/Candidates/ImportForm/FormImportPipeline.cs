using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Candidates.PriorApplications;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.ImportForm;

internal static class FormImportPipeline
{
    public static async Task<Result<ImportFormResponse>> ExecuteAsync(
        ImportFormCommand command,
        Vacancy vacancy,
        IntakeRound round,
        ParsedFormCsv parsedCsv,
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        FormLayout? layout;
        if (command.Layout is not null)
        {
            var layoutResult = await ApplyLayoutAsync(
                command.Layout,
                vacancy,
                parsedCsv.Headers,
                dbContext,
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
        var identityKeys = parsedCsv.Rows
            .Select(row => row.IdentityKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
        var priorApplicationsByKey = await PriorApplicationLookup.FindAsync(
            command.VacancyId,
            identityKeys,
            dbContext,
            cancellationToken);

        var plan = FormImportPlan.Build(
            parsedCsv.Rows,
            currentResponsesByKey.ToDictionary(
                pair => pair.Key,
                pair => new CurrentFormResponseState(
                    pair.Value.Candidate.Id,
                    pair.Value.Response.FormTimestampParsed),
                StringComparer.Ordinal),
            priorApplicationsByKey);

        var importedAt = timeProvider.GetUtcNow();
        foreach (var group in plan.Groups)
        {
            switch (group)
            {
                case CreateFormCandidatePlan create:
                {
                    var candidateResult = AddNewCandidate(
                        vacancy,
                        round,
                        create.RowsInMutationOrder,
                        importedAt,
                        layout,
                        dbContext);
                    if (candidateResult.IsFailure)
                    {
                        return Result<ImportFormResponse>.Failure(candidateResult.Error);
                    }

                    break;
                }

                case UpdateFormCandidatePlan update:
                {
                    var current = currentResponsesByKey[update.IdentityKey];
                    var response = current.Response;
                    foreach (var row in update.RowsInMutationOrder)
                    {
                        var addResult = current.Candidate.AddFormResponse(
                            ToResponseData(row, importedAt),
                            response,
                            isResubmitted: true);
                        if (addResult.IsFailure)
                        {
                            return Result<ImportFormResponse>.Failure(addResult.Error);
                        }

                        response = addResult.Value;
                    }

                    current.Candidate.PrefillDetailsFromLayout(layout);
                    break;
                }
            }
        }

        return new ImportFormResponse(
            parsedCsv.Rows.Count,
            plan.Created,
            plan.Updated,
            plan.SkippedOutdated,
            plan.PriorApplications);
    }

    private static async Task<Result<FormLayout>> ApplyLayoutAsync(
        FormLayoutDefinition definition,
        Vacancy vacancy,
        IReadOnlyList<string> headers,
        IApplicationDbContext dbContext,
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
            dbContext,
            cancellationToken);
        return layoutResult.Value;
    }

    private static async Task ReprojectActiveCandidatesAsync(
        Vacancy vacancy,
        FormLayout layout,
        IApplicationDbContext dbContext,
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

    private static Result<Candidate> AddNewCandidate(
        Vacancy vacancy,
        IntakeRound round,
        IReadOnlyList<ParsedFormRow> rowsInMutationOrder,
        DateTimeOffset importedAt,
        FormLayout layout,
        IApplicationDbContext dbContext)
    {
        var candidateResult = vacancy.ImportFormCandidate(new CandidateFormImportData(
            round.Id,
            importedAt));
        if (candidateResult.IsFailure)
        {
            return candidateResult;
        }

        var candidate = candidateResult.Value;
        CandidateFormResponse? currentResponse = null;
        foreach (var row in rowsInMutationOrder)
        {
            var addResult = candidate.AddFormResponse(
                ToResponseData(row, importedAt),
                currentResponse,
                isResubmitted: false);
            if (addResult.IsFailure)
            {
                return Result<Candidate>.Failure(addResult.Error);
            }

            currentResponse = addResult.Value;
        }

        candidate.PrefillDetailsFromLayout(layout);
        dbContext.Candidates.Add(candidate);
        return candidate;
    }

    private static CandidateFormResponseData ToResponseData(
        ParsedFormRow row,
        DateTimeOffset importedAt) => new(
        row.Cells,
        row.FormTimestampRaw,
        row.FormTimestampParsed,
        row.IdentityKey,
        importedAt);

    private sealed record CurrentFormResponse(
        CandidateFormResponse Response,
        Candidate Candidate);
}
