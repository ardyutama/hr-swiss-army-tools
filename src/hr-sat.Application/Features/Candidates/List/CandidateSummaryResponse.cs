using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.List;

internal sealed record CandidateSummaryResponse(
    long Id,
    string? FullName,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    string ReviewStatus,
    string ExtractionStatus,
    IReadOnlyList<CandidateSkillResponse> Skills,
    CandidateMatchResponse Match,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    DateTimeOffset? SourceSentAt)
{
    public static CandidateSummaryResponse From(
        Candidate candidate,
        IEnumerable<string> requirements)
    {
        var match = CandidateMatching.Calculate(
            requirements,
            candidate.Skills.Select(skill => skill.Phrase));
        return new CandidateSummaryResponse(
            candidate.Id,
            candidate.FullName,
            candidate.ContactEmail,
            candidate.ContactPhone,
            candidate.Notes,
            candidate.ReviewStatus.ToString().ToLowerInvariant(),
            candidate.ExtractionStatus.ToString().ToLowerInvariant(),
            candidate.Skills
                .OrderBy(skill => skill.Position)
                .Select(skill => new CandidateSkillResponse(
                    skill.Id,
                    skill.Phrase,
                    skill.Position))
                .ToList(),
            CandidateMatchResponse.From(match),
            candidate.SourceSenderName,
            candidate.SourceSenderEmail,
            candidate.SourceSubject,
            candidate.SourceSentAt);
    }
}
