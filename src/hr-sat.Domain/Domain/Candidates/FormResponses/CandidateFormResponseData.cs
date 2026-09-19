namespace hr_sat.Domain.Candidates.FormResponses;

public sealed record CandidateFormResponseData(
    IReadOnlyList<string> Cells,
    string FormTimestampRaw,
    DateTimeOffset? FormTimestampParsed,
    string? IdentityKey,
    DateTimeOffset ImportedAt);