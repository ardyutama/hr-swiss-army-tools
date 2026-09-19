using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Shared;
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
        var contexts = await ScreeningContextLoader.LoadLiveAsync(
            vacancyIds,
            dbContext,
            cancellationToken);

        return vacancies
            .Select(vacancy => ProjectSummary(
                vacancy,
                rounds.Where(round => round.VacancyId == vacancy.Id).ToArray(),
                candidates,
                contexts))
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
        var context = await ScreeningContextLoader.LoadLiveAsync(
            [vacancy.Id],
            dbContext,
            cancellationToken);
        var liveContext = context.GetValueOrDefault(vacancy.Id, ScreeningContext.None);
        var candidateRows = rounds
            .SelectMany(round => candidates
                .Where(candidate => candidate.IntakeRoundId == round.Id)
                .Select(candidate => new CandidateRound(
                    candidate,
                    !round.IsOpen ? ScreeningContext.Frozen : liveContext)))
            .ToArray();
        var progress = BuildProgress(candidateRows);
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
        IReadOnlyDictionary<long, ScreeningContext> contexts)
    {
        var liveContext = contexts.GetValueOrDefault(vacancy.Id, ScreeningContext.None);
        var candidateRows = rounds
            .SelectMany(round => candidates
                .Where(candidate => candidate.IntakeRoundId == round.Id)
                .Select(candidate => new CandidateRound(
                    candidate,
                    !round.IsOpen ? ScreeningContext.Frozen : liveContext)))
            .ToArray();

        return new VacancySummaryResponse(
            vacancy.Id,
            vacancy.Title,
            vacancy.OpenedOn,
            vacancy.Status == VacancyStatus.Open ? "open" : "closed",
            BuildProgress(candidateRows),
            BuildReviewCounts(candidateRows),
            BuildHiring(candidateRows, vacancy.NeededHires));
    }

    private static VacancyProgressResponse BuildProgress(
        IReadOnlyList<CandidateRound> candidateRows)
    {
        var visibleCandidates = candidateRows
            .Where(row => !row.ScreenedOut)
            .ToArray();

        return new VacancyProgressResponse(
            visibleCandidates.Count(row =>
                row.Candidate.ReviewStatus is
                    CandidateReviewStatus.Shortlisted or CandidateReviewStatus.Rejected),
            visibleCandidates.Length);
    }

    private static VacancyReviewCountsResponse BuildReviewCounts(
        IReadOnlyList<CandidateRound> candidateRows)
    {
        var visibleCandidates = candidateRows
            .Where(row => !row.ScreenedOut)
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

    private sealed record CandidateRound(Candidate Candidate, ScreeningContext Context)
    {
        public bool ScreenedOut { get; } = Candidate.EvaluateScreening(Context).Count > 0;
    }
}