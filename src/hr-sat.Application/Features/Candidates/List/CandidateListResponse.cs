namespace hr_sat.Application.Features.Candidates.List;

public sealed record CandidateListResponse(
    IReadOnlyList<CandidateSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total,
    int FilteredTotal,
    CandidateListCountsResponse Counts);

public sealed record CandidateListCountsResponse(
    CandidateStatusCountsResponse Status,
    CandidateOutcomeCountsResponse Outcome,
    int ScreenedOut);

public sealed record CandidateStatusCountsResponse(
    int New,
    int Flagged,
    int Shortlisted,
    int Rejected);

public sealed record CandidateOutcomeCountsResponse(
    int Any,
    int Undecided,
    int Hired,
    int Runaway,
    int Declined);