using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.ScreeningRules.Preview;

internal sealed class PreviewScreeningRulesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<PreviewScreeningRulesQuery, ScreeningRulesPreviewResponse>
{
    public async Task<Result<ScreeningRulesPreviewResponse>> Handle(
        PreviewScreeningRulesQuery query,
        CancellationToken cancellationToken)
    {
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.VacancyId == query.VacancyId,
                cancellationToken);
        if (layout is null || !layout.IsValid)
        {
            return Result<ScreeningRulesPreviewResponse>.Failure(
                ScreeningRuleSetErrors.SnapshotRequired(query.VacancyId));
        }

        var ruleSetResult = ScreeningRuleSet.Create(
            query.VacancyId,
            layout,
            new ScreeningRuleDefinition(query.Rules));
        if (ruleSetResult.IsFailure)
        {
            return Result<ScreeningRulesPreviewResponse>.Failure(ruleSetResult.Error);
        }

        var ruleSet = ruleSetResult.Value;
        var activeRoundId = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round => round.VacancyId == query.VacancyId && round.ClosedAt == null)
            .Select(round => (long?)round.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!activeRoundId.HasValue)
        {
            return EmptyPreview(ruleSet);
        }

        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate =>
                candidate.IntakeRoundId == activeRoundId.Value &&
                candidate.IntakeSource == CandidateIntakeSource.Form)
            .Include(candidate => candidate.FormResponses)
            .ToListAsync(cancellationToken);

        var firedRules = candidates
            .Select(candidate => candidate.EvaluateScreening(false, ruleSet, layout))
            .ToArray();
        var perRule = ruleSet.Rules
            .Select((_, index) => new ScreeningRulePreviewResponse(
                index,
                firedRules.Count(fired => fired.Any(rule => rule.Index == index))))
            .ToArray();

        return new ScreeningRulesPreviewResponse(
            candidates.Count,
            firedRules.Count(fired => fired.Count > 0),
            perRule);
    }

    private static Result<ScreeningRulesPreviewResponse> EmptyPreview(
        ScreeningRuleSet ruleSet) =>
        new ScreeningRulesPreviewResponse(
            0,
            0,
            ruleSet.Rules
                .Select((_, index) => new ScreeningRulePreviewResponse(index, 0))
                .ToArray());
}