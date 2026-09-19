using hr_sat.Domain.Vacancies.FormLayouts;

namespace hr_sat.Application.Features.FormLayouts;

public sealed record FormLayoutResponse(
    long Id,
    long VacancyId,
    IReadOnlyList<string> HeaderSnapshot,
    IReadOnlyList<FormLayoutColumnResponse> Columns,
    bool IsValid,
    int CandidatesUpdated = 0,
    int TypedOverridesKept = 0)
{
    public static FormLayoutResponse From(
        FormLayout layout,
        int candidatesUpdated = 0,
        int typedOverridesKept = 0) => new(
        layout.Id,
        layout.VacancyId,
        layout.HeaderSnapshot,
        layout.Columns
            .OrderBy(column => column.Ordinal)
            .Select(column => new FormLayoutColumnResponse(
                column.Ordinal,
                column.Role?.ToString().ToLowerInvariant(),
                column.Label))
            .ToArray(),
        layout.IsValid,
        candidatesUpdated,
        typedOverridesKept);
}

public sealed record FormLayoutColumnResponse(
    int Ordinal,
    string? Role,
    string? Label);