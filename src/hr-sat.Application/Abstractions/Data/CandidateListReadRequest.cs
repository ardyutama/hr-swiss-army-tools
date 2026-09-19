namespace hr_sat.Application.Abstractions.Data;

public sealed record CandidateListReadRequest(
    long RoundId,
    bool RoundClosed,
    string? Status,
    string? Outcome,
    string? Query,
    string? Sort,
    bool IncludeScreenedOut,
    int Page);