using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.FormLayouts.Get;

internal sealed class GetFormLayoutQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetFormLayoutQuery, FormLayoutResponse>
{
    public async Task<Result<FormLayoutResponse>> Handle(
        GetFormLayoutQuery query,
        CancellationToken cancellationToken)
    {
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.VacancyId == query.VacancyId,
                cancellationToken);
        return layout is null
            ? Result<FormLayoutResponse>.Failure(FormLayoutErrors.NotFound(query.VacancyId))
            : FormLayoutResponse.From(layout);
    }
}