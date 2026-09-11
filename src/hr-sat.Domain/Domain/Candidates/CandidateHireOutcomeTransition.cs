namespace hr_sat.Domain.Candidates;

internal sealed record CandidateHireOutcomeTransition(
    CandidateHireOutcome Outcome,
    string? Notes);