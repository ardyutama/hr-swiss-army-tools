using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.ReviewQueue;

public sealed record GetReviewQueueQuery(long VacancyId, long RoundId)
    : IQuery<IReadOnlyList<CandidateSummaryResponse>>
{
    public GetReviewQueueQuery(
        long vacancyId,
        long roundId,
        string? status,
        string? outcome,
        string? query,
        string? sort,
        string? screened)
        : this(vacancyId, roundId)
    {
        Status = status;
        Outcome = outcome;
        Query = query;
        Sort = sort;
        Screened = screened;
    }

    public string? Status { get; } = null;
    public string? Outcome { get; } = null;
    public string? Query { get; } = null;
    public string? Sort { get; } = null;
    public string? Screened { get; } = null;
}