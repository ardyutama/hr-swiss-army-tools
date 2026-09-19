using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class ListVacanciesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<ListVacanciesQuery, IReadOnlyList<VacancySummaryResponse>>
{
    public async Task<Result<IReadOnlyList<VacancySummaryResponse>>> Handle(
        ListVacanciesQuery query,
        CancellationToken cancellationToken)
    {
        var vacancies = await VacancyProgress.GetSummariesAsync(
            dbContext,
            cancellationToken);

        return Result<IReadOnlyList<VacancySummaryResponse>>.Success(vacancies);
    }
}
