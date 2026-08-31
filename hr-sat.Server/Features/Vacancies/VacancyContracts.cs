using System.ComponentModel.DataAnnotations;
using hr_sat.Server.Domain.Vacancies;

namespace hr_sat.Server.Features.Vacancies;

// Presence rules are the input boundary (AddValidation pipeline, 400 before the handler).
// Normalization-dependent rules (trim, uniqueness, ordering) stay in the domain.
internal sealed record VacancyDefinitionRequest(
    [property: Required(ErrorMessage = "Title is required.")]
    string? Title,
    DateOnly OpenedOn,
    [property: Required(ErrorMessage = "At least one vacancy requirement is required.")]
    [property: MinLength(1, ErrorMessage = "At least one vacancy requirement is required.")]
    IReadOnlyList<string?>? Requirements);

internal sealed record VacancyRequirementResponse(long Id, string Phrase, int Position);

internal sealed record VacancyProgressResponse(int ProcessedCandidates, int TotalCandidates);

internal sealed record VacancyDetailsResponse(
    long Id,
    string Title,
    DateOnly OpenedOn,
    string Status,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<VacancyRequirementResponse> Requirements,
    VacancyProgressResponse Progress)
{
    public static VacancyDetailsResponse From(
        Vacancy vacancy,
        VacancyProgressResponse? progress = null) => new(
        vacancy.Id,
        vacancy.Title,
        vacancy.OpenedOn,
        vacancy.Status.ToString().ToLowerInvariant(),
        vacancy.ClosedAt,
        vacancy.CreatedAt,
        vacancy.Requirements
            .OrderBy(requirement => requirement.Position)
            .Select(requirement => new VacancyRequirementResponse(
                requirement.Id,
                requirement.Phrase,
                requirement.Position))
            .ToList(),
        progress ?? new VacancyProgressResponse(0, 0));
}

internal sealed record VacancySummaryResponse(
    long Id,
    string Title,
    DateOnly OpenedOn,
    string Status,
    VacancyProgressResponse Progress);