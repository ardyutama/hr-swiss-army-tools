using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class ListVacancies
{
    public static async Task<IReadOnlyList<VacancySummaryResponse>> HandleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Vacancies
            .AsNoTracking()
            .OrderBy(vacancy => vacancy.CreatedAt)
            .ThenBy(vacancy => vacancy.Id)
            .Select(vacancy => new VacancySummaryResponse(
                vacancy.Id,
                vacancy.Title,
                vacancy.OpenedOn,
                vacancy.Status == Domain.Vacancies.VacancyStatus.Open ? "open" : "closed",
                new VacancyProgressResponse(
                    vacancy.Candidates.Count(candidate =>
                        candidate.ReviewStatus == CandidateReviewStatus.Shortlisted ||
                        candidate.ReviewStatus == CandidateReviewStatus.Rejected),
                    vacancy.Candidates.Count())))
            .ToListAsync(cancellationToken);
}