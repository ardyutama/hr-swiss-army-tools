namespace hr_sat.Domain.Candidates;

internal sealed record CandidateExtraction(
    string? FullName,
    string? ContactEmail,
    string? ContactPhone,
    IReadOnlyList<string> Skills);
