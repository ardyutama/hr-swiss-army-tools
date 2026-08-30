using hr_sat.Server.Domain.Candidates;

namespace hr_sat.Server.Features.Candidates.Import;

internal sealed record CandidateImportResponse(
    long Id,
    string ReviewStatus,
    string ExtractionStatus,
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
        candidate.ExtractionStatus.ToString().ToLowerInvariant(),
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

internal sealed record CvDocumentResponse(
    long Id,
    string OriginalFilename,
    long SizeBytes,
    bool IsPrimary,
    string DownloadUrl);

internal sealed record ImportFileResponse(
    string FileName,
    string Status,
    string? Error,
    CandidateImportResponse? Candidate);

internal sealed record ImportCandidatesResponse(IReadOnlyList<ImportFileResponse> Results);