using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.Import;

public sealed record CandidateImportResponse(
    long Id,
    string ReviewStatus,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    string? SourceBodyText,
    DateTimeOffset? SourceSentAt,
    string SourceOriginalFilename,
    IReadOnlyList<CvDocumentResponse> Documents)
{
    public static CandidateImportResponse From(long vacancyId, Candidate candidate) => new(
        candidate.Id,
        candidate.ReviewStatus.ToString().ToLowerInvariant(),
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

public sealed record ImportFileResponse(
    string FileName,
    string Status,
    string? Error,
    CandidateImportResponse? Candidate);

public sealed record ImportCandidatesResponse(IReadOnlyList<ImportFileResponse> Results);