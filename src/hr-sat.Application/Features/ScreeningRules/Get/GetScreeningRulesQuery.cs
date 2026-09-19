using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.ScreeningRules.Get;

public sealed record GetScreeningRulesQuery(long VacancyId)
    : IQuery<ScreeningRuleSetResponse>;