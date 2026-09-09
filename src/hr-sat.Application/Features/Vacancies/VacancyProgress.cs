using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal static class VacancyProgress
{
    public static IQueryable<VacancySummaryResponse> ProjectSummaries(
        IQueryable<Vacancy> vacancies) =>
        vacancies.Select(vacancy => new VacancySummaryResponse(
            vacancy.Id,
            vacancy.Title,
            vacancy.OpenedOn,
            vacancy.Status == VacancyStatus.Open ? "open" : "closed",
            new VacancyProgressResponse(
                vacancy.Rounds.SelectMany(round => round.Candidates).Count(candidate =>
                    candidate.ReviewStatus == CandidateReviewStatus.Shortlisted ||
                    candidate.ReviewStatus == CandidateReviewStatus.Rejected),
                vacancy.Rounds.SelectMany(round => round.Candidates).Count()),
            vacancy.NeededHires.HasValue
                ? new VacancyHiringResponse(
                    vacancy.NeededHires.Value,
                        vacancy.Rounds.SelectMany(round => round.Candidates).Count(candidate =>
                            candidate.HireOutcome == CandidateHireOutcome.Hired))
                : null));

    public static async Task<VacancyProgressResponse> GetAsync(
        long vacancyId,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var progress = await ProjectSummaries(dbContext.Vacancies
                .AsNoTracking()
                .Where(vacancy => vacancy.Id == vacancyId))
            .Select(vacancy => vacancy.Progress)
            .SingleOrDefaultAsync(cancellationToken);

        return progress ?? new VacancyProgressResponse(0, 0);
    }

    public static async Task<VacancyDetailsResponse> GetDetailsAsync(
        Vacancy vacancy,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var summary = await ProjectSummaries(dbContext.Vacancies
                .AsNoTracking()
                .Where(item => item.Id == vacancy.Id))
            .SingleOrDefaultAsync(cancellationToken);
        var progress = summary?.Progress ?? new VacancyProgressResponse(0, 0);
        var rounds = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round => round.VacancyId == vacancy.Id)
            .OrderBy(round => round.RoundNumber)
            .Select(round => new VacancyRoundResponse(
                round.Id,
                round.RoundNumber,
                round.Name,
                round.IsOpen ? "open" : "closed",
                round.ClosedAt,
                round.Candidates.Count()))
            .ToListAsync(cancellationToken);

        return VacancyDetailsResponse.From(vacancy, progress, rounds, summary?.Hiring);
    }
}