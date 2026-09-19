using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal static class VacancyProgress
{
    public static async Task<IReadOnlyList<VacancySummaryResponse>> GetSummariesAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var vacancies = await dbContext.Vacancies
            .AsNoTracking()
            .OrderBy(vacancy => vacancy.CreatedAt)
            .ThenBy(vacancy => vacancy.Id)
            .ToListAsync(cancellationToken);
        var vacancyIds = vacancies.Select(vacancy => vacancy.Id).ToArray();
        var rounds = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round => vacancyIds.Contains(round.VacancyId))
            .ToListAsync(cancellationToken);
        var roundIds = rounds.Select(round => round.Id).ToArray();
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => roundIds.Contains(candidate.IntakeRoundId))
            .Include(candidate => candidate.FormResponses)
            .ToListAsync(cancellationToken);
        var layouts = await dbContext.FormLayouts
            .AsNoTracking()
            .Where(layout => vacancyIds.Contains(layout.VacancyId))
            .ToDictionaryAsync(layout => layout.VacancyId, cancellationToken);
        var ruleSets = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .Where(ruleSet => vacancyIds.Contains(ruleSet.VacancyId))
            .ToDictionaryAsync(ruleSet => ruleSet.VacancyId, cancellationToken);

        return vacancies
            .Select(vacancy => ProjectSummary(
                vacancy,
                rounds.Where(round => round.VacancyId == vacancy.Id).ToArray(),
                candidates,
                layouts.GetValueOrDefault(vacancy.Id),
                ruleSets.GetValueOrDefault(vacancy.Id)))
            .ToArray();
    }

    public static async Task<VacancyDetailsResponse> GetDetailsAsync(
        Vacancy vacancy,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rounds = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round => round.VacancyId == vacancy.Id)
            .OrderBy(round => round.RoundNumber)
            .ToListAsync(cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => dbContext.IntakeRounds.Any(round =>
                round.Id == candidate.IntakeRoundId &&
                round.VacancyId == vacancy.Id))
            .Include(candidate => candidate.FormResponses)
            .ToListAsync(cancellationToken);
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == vacancy.Id, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == vacancy.Id, cancellationToken);
        var candidateRows = rounds
            .SelectMany(round => candidates
                .Where(candidate => candidate.IntakeRoundId == round.Id)
                .Select(candidate => new CandidateRound(candidate, !round.IsOpen)))
            .ToArray();
        var progress = BuildProgress(candidateRows, ruleSet, layout);
        var hiring = BuildHiring(candidateRows, vacancy.NeededHires);

        return VacancyDetailsResponse.From(
            vacancy,
            progress,
            rounds.Select(round => new VacancyRoundResponse(
                round.Id,
                round.RoundNumber,
                round.Name,
                round.IsOpen ? "open" : "closed",
                round.ClosedAt,
                candidates.Count(candidate => candidate.IntakeRoundId == round.Id))).ToArray(),
            hiring);
    }

    private static VacancySummaryResponse ProjectSummary(
        Vacancy vacancy,
        IReadOnlyList<IntakeRound> rounds,
        IReadOnlyList<Candidate> candidates,
        FormLayout? layout,
        ScreeningRuleSet? ruleSet)
    {
        var candidateRows = rounds
            .SelectMany(round => candidates
                .Where(candidate => candidate.IntakeRoundId == round.Id)
                .Select(candidate =>
                new CandidateRound(candidate, !round.IsOpen)))
            .ToArray();

        return new VacancySummaryResponse(
            vacancy.Id,
            vacancy.Title,
            vacancy.OpenedOn,
            vacancy.Status == VacancyStatus.Open ? "open" : "closed",
            BuildProgress(candidateRows, ruleSet, layout),
            BuildReviewCounts(candidateRows, ruleSet, layout),
            BuildHiring(candidateRows, vacancy.NeededHires));
    }

    private static VacancyProgressResponse BuildProgress(
        IReadOnlyList<CandidateRound> candidateRows,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout)
    {
        var visibleCandidates = candidateRows
            .Where(row => !IsScreenedOut(row, ruleSet, layout))
            .ToArray();

        return new VacancyProgressResponse(
            visibleCandidates.Count(row =>
                row.Candidate.ReviewStatus is
                    CandidateReviewStatus.Shortlisted or CandidateReviewStatus.Rejected),
            visibleCandidates.Length);
    }

    private static VacancyReviewCountsResponse BuildReviewCounts(
        IReadOnlyList<CandidateRound> candidateRows,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout)
    {
        var visibleCandidates = candidateRows
            .Where(row => !IsScreenedOut(row, ruleSet, layout))
            .Select(row => row.Candidate)
            .ToArray();

        return new VacancyReviewCountsResponse(
            visibleCandidates.Count(candidate => candidate.ReviewStatus == CandidateReviewStatus.New),
            visibleCandidates.Count(candidate => candidate.ReviewStatus == CandidateReviewStatus.Flagged),
            visibleCandidates.Count(candidate => candidate.ReviewStatus == CandidateReviewStatus.Shortlisted),
            visibleCandidates.Count(candidate => candidate.ReviewStatus == CandidateReviewStatus.Rejected));
    }

    private static VacancyHiringResponse? BuildHiring(
        IReadOnlyList<CandidateRound> candidateRows,
        int? neededHires) =>
        neededHires.HasValue
            ? new VacancyHiringResponse(
                neededHires.Value,
                candidateRows.Count(row => row.Candidate.HireOutcome == CandidateHireOutcome.Hired))
            : null;

    private static bool IsScreenedOut(
        CandidateRound candidateRow,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout) =>
        candidateRow.Candidate
            .EvaluateScreening(candidateRow.RoundClosed, ruleSet, layout)
            .Count > 0;

    private sealed record CandidateRound(Candidate Candidate, bool RoundClosed);
}