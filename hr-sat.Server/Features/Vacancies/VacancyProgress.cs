using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class VacancyProgress
{
    public static async Task<VacancyProgressResponse> GetAsync(
        long vacancyId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var progress = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.VacancyId == vacancyId)
            .GroupBy(_ => true)
            .Select(group => new VacancyProgressResponse(
                group.Count(candidate =>
                    candidate.ReviewStatus == CandidateReviewStatus.Shortlisted ||
                    candidate.ReviewStatus == CandidateReviewStatus.Rejected),
                group.Count()))
            .SingleOrDefaultAsync(cancellationToken);

        return progress ?? new VacancyProgressResponse(0, 0);
    }
}