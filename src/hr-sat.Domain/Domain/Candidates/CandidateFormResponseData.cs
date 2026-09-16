namespace hr_sat.Domain.Candidates;

public sealed record CandidateFormResponseData(
    IReadOnlyList<string> Cells,
    string FormTimestampRaw,
    DateTimeOffset? FormTimestampParsed,
    string? IdentityKey,
    DateTimeOffset ImportedAt);