using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Vacancies;

public sealed record VacancyRequirementResponse(long Id, string Phrase, int Position);

public sealed record VacancyProgressResponse(int ProcessedCandidates, int TotalCandidates);

public sealed record VacancyHiringResponse(int NeededHires, int ActiveHires);

public sealed record VacancyRoundResponse(
    long Id,
    int RoundNumber,
    string? Name,
    string Status,
    DateTimeOffset? ClosedAt,
    int CandidateCount);

public sealed record VacancyDetailsResponse(
    long Id,
    string Title,
    DateOnly OpenedOn,
    string Status,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<VacancyRequirementResponse> Requirements,
    IReadOnlyList<VacancyRoundResponse> Rounds,
    VacancyProgressResponse Progress,
    VacancyHiringResponse? Hiring)
{
    public static VacancyDetailsResponse From(
        Vacancy vacancy,
        VacancyProgressResponse? progress = null,
        IReadOnlyList<VacancyRoundResponse>? rounds = null,
        VacancyHiringResponse? hiring = null)
    {
        var resolvedHiring = hiring ?? (vacancy.NeededHires.HasValue
            ? new VacancyHiringResponse(vacancy.NeededHires.Value, 0)
            : null);

        return new VacancyDetailsResponse(
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
            rounds ?? vacancy.Rounds
                .OrderBy(round => round.RoundNumber)
                .Select(round => new VacancyRoundResponse(
                    round.Id,
                    round.RoundNumber,
                    round.Name,
                    round.IsOpen ? "open" : "closed",
                    round.ClosedAt,
                    round.Candidates.Count))
                .ToList(),
            progress ?? new VacancyProgressResponse(0, 0),
            resolvedHiring);
    }
}

public sealed record VacancySummaryResponse(
    long Id,
    string Title,
    DateOnly OpenedOn,
    string Status,
    VacancyProgressResponse Progress,
    VacancyHiringResponse? Hiring);