namespace hr_sat.Application.Features.Candidates.Shared;

internal static class CandidateMatching
{
    public static CandidateMatch Calculate(
        IEnumerable<string> requirements,
        IEnumerable<string> skills)
    {
        var normalizedSkills = skills
            .Select(Normalize)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requirementList = requirements.ToArray();
        var matchedRequirements = requirementList.Count(requirement =>
            normalizedSkills.Contains(Normalize(requirement)));

        return new CandidateMatch(matchedRequirements, requirementList.Length);
    }

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

internal sealed record CandidateMatch(int MatchedRequirements, int TotalRequirements);
