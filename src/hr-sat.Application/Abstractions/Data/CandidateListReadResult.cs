namespace hr_sat.Application.Abstractions.Data;

public sealed record CandidateListReadResult(
    IReadOnlyList<CandidateListReadRow> Rows,
    int Total,
    int FilteredTotal,
    CandidateListReadCounts Counts);