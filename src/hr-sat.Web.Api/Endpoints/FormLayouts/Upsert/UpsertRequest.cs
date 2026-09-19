using hr_sat.Domain.Vacancies.FormLayouts;

namespace hr_sat.Web.Api.Endpoints.FormLayouts;

internal sealed record UpsertRequest(
    IReadOnlyList<FormLayoutColumn>? Columns);