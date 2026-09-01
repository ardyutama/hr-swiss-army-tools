namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed record UpdateRequest(
    string? Title,
    DateOnly OpenedOn,
    IReadOnlyList<string?>? Requirements);