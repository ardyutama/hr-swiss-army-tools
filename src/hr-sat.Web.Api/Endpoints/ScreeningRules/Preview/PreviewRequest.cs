using hr_sat.Domain.Vacancies;

namespace hr_sat.Web.Api.Endpoints.ScreeningRules;

internal sealed record PreviewRequest(
    IReadOnlyList<ScreeningRule>? Rules);