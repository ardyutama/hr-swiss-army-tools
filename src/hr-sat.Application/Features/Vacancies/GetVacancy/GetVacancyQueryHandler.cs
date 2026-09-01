using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class GetVacancyQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetVacancyQuery, VacancyDetailsResponse>
{
    public async Task<Result<VacancyDetailsResponse>> Handle(
        GetVacancyQuery query,
        CancellationToken cancellationToken)
    {
        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Include(item => item.Requirements)
            .SingleOrDefaultAsync(item => item.Id == query.Id, cancellationToken);

        if (vacancy is null)
        {
            return Result<VacancyDetailsResponse>.Failure(VacancyErrors.NotFound(query.Id));
        }

        return await VacancyProgress.GetDetailsAsync(vacancy, dbContext, cancellationToken);
    }
}
