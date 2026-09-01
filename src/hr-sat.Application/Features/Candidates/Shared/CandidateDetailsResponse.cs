using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.Shared;

public sealed record CandidateDetailsResponse(
    long Id,
    string ReviewStatus,
    string ExtractionStatus,
    string? FullName,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    IReadOnlyList<CandidateSkillResponse> Skills,
    CandidateMatchResponse Match,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    string? SourceBodyText,
    DateTimeOffset? SourceSentAt,
    string SourceOriginalFilename,
    IReadOnlyList<CvDocumentResponse> Documents)
{
    public static CandidateDetailsResponse From(
        long vacancyId,
        Candidate candidate,
        IEnumerable<string> requirements) => new(
        candidate.Id,
        candidate.ReviewStatus.ToString().ToLowerInvariant(),
        candidate.ExtractionStatus.ToString().ToLowerInvariant(),
        candidate.FullName,
        candidate.ContactEmail,
        candidate.ContactPhone,
        candidate.Notes,
        candidate.Skills
            .OrderBy(skill => skill.Position)
            .Select(skill => new CandidateSkillResponse(
                skill.Id,
                skill.Phrase,
                skill.Position))
            .ToList(),
        CandidateMatchResponse.From(CandidateMatching.Calculate(
            requirements,
            candidate.Skills.Select(skill => skill.Phrase))),
        candidate.SourceSenderName,
        candidate.SourceSenderEmail,
        candidate.SourceSubject,
        candidate.SourceBodyText,
        candidate.SourceSentAt,
        candidate.SourceOriginalFilename,
        candidate.CvDocuments
            .OrderBy(document => document.Position)
            .Select(document => new CvDocumentResponse(
                document.Id,
                document.OriginalFilename,
                document.SizeBytes,
                document.IsPrimary,
                $"/api/vacancies/{vacancyId}/candidates/{candidate.Id}/cv-documents/{document.Id}"))
            .ToList());
}

public sealed record CvDocumentResponse(
    long Id,
    string OriginalFilename,
    long SizeBytes,
    bool IsPrimary,
    string DownloadUrl);
