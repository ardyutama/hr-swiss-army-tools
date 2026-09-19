namespace hr_sat.Application.Abstractions.Data;

public sealed record CandidateListReadResult(
    IReadOnlyList<long> CandidateIds,
    int Total,
    int FilteredTotal,
    CandidateListReadCounts Counts);