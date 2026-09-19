using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.List;

public sealed record ListCandidatesQuery(long VacancyId, long RoundId)
    : IQuery<CandidateListResponse>
{
    public ListCandidatesQuery(
        long vacancyId,
        long roundId,
        string? status,
        string? outcome,
        string? query,
        string? sort,
        string? screened,
        int page)
        : this(vacancyId, roundId)
    {
        Status = status;
        Outcome = outcome;
        Query = query;
        Sort = sort;
        Screened = screened;
        Page = page;
    }

    public string? Status { get; } = null;
    public string? Outcome { get; } = null;
    public string? Query { get; } = null;
    public string? Sort { get; } = null;
    public string? Screened { get; } = null;
    public int Page { get; } = 1;
}
