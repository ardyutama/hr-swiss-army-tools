using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.ScreeningRules.Preview;

public sealed record PreviewScreeningRulesQuery(
    long VacancyId,
    IReadOnlyList<ScreeningRule>? Rules)
    : IQuery<ScreeningRulesPreviewResponse>;