using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.ScreeningRules.Upsert;

public sealed record UpsertScreeningRulesCommand(
    long VacancyId,
    IReadOnlyList<ScreeningRule>? Rules)
    : ICommand<ScreeningRuleSetResponse>;