using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class ListVacanciesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<ListVacanciesQuery, IReadOnlyList<VacancySummaryResponse>>
{
    public async Task<Result<IReadOnlyList<VacancySummaryResponse>>> Handle(
        ListVacanciesQuery query,
        CancellationToken cancellationToken)
    {
        var vacancies = await VacancyProgress.ProjectSummaries(dbContext.Vacancies
                .AsNoTracking()
                .OrderBy(vacancy => vacancy.CreatedAt)
                .ThenBy(vacancy => vacancy.Id))
            .ToListAsync(cancellationToken);

        return vacancies;
    }
}
