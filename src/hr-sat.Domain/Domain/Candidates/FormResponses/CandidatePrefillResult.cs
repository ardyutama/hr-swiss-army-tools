namespace hr_sat.Domain.Candidates.FormResponses;

public sealed record CandidatePrefillResult(
    bool Updated,
    int TypedOverridesKept);
