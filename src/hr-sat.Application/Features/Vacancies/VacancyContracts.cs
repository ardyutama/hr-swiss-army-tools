using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Vacancies;

public sealed record VacancyRequirementResponse(long Id, string Phrase, int Position);

public sealed record VacancyProgressResponse(int ProcessedCandidates, int TotalCandidates);

public sealed record VacancyDetailsResponse(
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

public sealed record VacancySummaryResponse(
    long Id,
    string Title,
    DateOnly OpenedOn,
    string Status,
    VacancyProgressResponse Progress);