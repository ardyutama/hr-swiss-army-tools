namespace hr_sat.Application.Features.Candidates;

public sealed record CandidateDetailsResponse(
    long Id,
    string ReviewStatus,
    string? FullName,
    string? ContactEmail,
    string? Notes,
    IReadOnlyList<CandidateRequirementReviewResponse> RequirementReviews,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    string? SourceBodyText,
    DateTimeOffset? SourceSentAt,
    string SourceOriginalFilename,
    IReadOnlyList<CandidateDocumentResponse> Documents);

public sealed record CandidateRequirementReviewResponse(long RequirementId, bool Confirmed);

public sealed record CandidateDocumentResponse(
    long Id,
    string OriginalFilename,
    long SizeBytes,
    bool IsPrimary,
    string DownloadUrl);