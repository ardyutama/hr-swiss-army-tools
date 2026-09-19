using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.ScreeningRules.Get;

internal sealed class GetScreeningRulesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetScreeningRulesQuery, ScreeningRuleSetResponse>
{
    public async Task<Result<ScreeningRuleSetResponse>> Handle(
        GetScreeningRulesQuery query,
        CancellationToken cancellationToken)
    {
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.VacancyId == query.VacancyId,
                cancellationToken);
        return ruleSet is null
            ? Result<ScreeningRuleSetResponse>.Failure(
                ScreeningRuleSetErrors.NotFound(query.VacancyId))
            : ScreeningRuleSetResponse.From(ruleSet);
    }
}