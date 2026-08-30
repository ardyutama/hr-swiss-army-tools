using hr_sat.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class ListVacancies
{
    public static async Task<IReadOnlyList<VacancySummaryResponse>> HandleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => await VacancyProgress.ProjectSummaries(dbContext.Vacancies
                .AsNoTracking()
                .OrderBy(vacancy => vacancy.CreatedAt)
                .ThenBy(vacancy => vacancy.Id))
            .ToListAsync(cancellationToken);
}