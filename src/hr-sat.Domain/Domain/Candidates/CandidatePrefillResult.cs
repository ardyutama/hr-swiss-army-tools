namespace hr_sat.Domain.Candidates;

public sealed record CandidatePrefillResult(
    bool Updated,
    int TypedOverridesKept);
