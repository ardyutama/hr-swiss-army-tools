namespace hr_sat.Application.Abstractions.Data;

public sealed record CandidateListReadCounts(
    int New,
    int Flagged,
    int Shortlisted,
    int Rejected,
    int Any,
    int Undecided,
    int Hired,
    int Runaway,
    int Declined,
    int ScreenedOut);