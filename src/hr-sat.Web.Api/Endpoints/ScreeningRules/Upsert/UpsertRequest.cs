using hr_sat.Domain.Vacancies;

namespace hr_sat.Web.Api.Endpoints.ScreeningRules;

internal sealed record UpsertRequest(
    IReadOnlyList<ScreeningRule>? Rules);