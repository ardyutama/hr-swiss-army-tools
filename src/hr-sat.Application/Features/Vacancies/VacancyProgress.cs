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
                vacancy.Candidates.Count(candidate =>
                    candidate.ReviewStatus == CandidateReviewStatus.Shortlisted ||
                    candidate.ReviewStatus == CandidateReviewStatus.Rejected),
                vacancy.Candidates.Count())));

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
        var progress = await GetAsync(vacancy.Id, dbContext, cancellationToken);
        return VacancyDetailsResponse.From(vacancy, progress);
    }
}