namespace hr_sat.Application.Features.Candidates;

public sealed record CandidatePriorApplicationResponse(
    int RoundNumber,
    string? RoundName,
    string ReviewStatus);