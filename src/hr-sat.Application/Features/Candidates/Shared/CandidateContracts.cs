namespace hr_sat.Application.Features.Candidates.Shared;

public sealed record CandidateSkillResponse(
    long Id,
    string Phrase,
    int Position);

public sealed record CandidateMatchResponse(
    int MatchedRequirements,
    int TotalRequirements)
{
    internal static CandidateMatchResponse From(CandidateMatch match) =>
        new(match.MatchedRequirements, match.TotalRequirements);
}
