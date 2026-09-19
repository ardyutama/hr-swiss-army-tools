namespace hr_sat.Application.Features.Candidates;

public sealed record CandidateDetailsResponse(
    long Id,
    string ReviewStatus,
    string HireOutcome,
    int? PromotedFromRoundNumber,
    DateTimeOffset? PromotedAt,
    IReadOnlyList<CandidatePriorApplicationResponse> PriorApplications,
    string? FullName,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    IReadOnlyList<CandidateRequirementReviewResponse> RequirementReviews,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    string? SourceBodyText,
    DateTimeOffset? SourceSentAt,
    string? SourceOriginalFilename,
    IReadOnlyList<CandidateDocumentResponse> Documents,
    string IntakeSource,
    bool IsResubmitted,
    IReadOnlyList<CandidateFormResponseResponse> FormResponses,
    CandidateScreeningResponse? Screening = null);

public sealed record CandidateRequirementReviewResponse(long RequirementId, bool Confirmed);

public sealed record CandidateDocumentResponse(
    long Id,
    string OriginalFilename,
    long SizeBytes,
    bool IsPrimary,
    string DownloadUrl);

public sealed record CandidateFormResponseResponse(
    IReadOnlyList<string> Cells,
    string FormTimestampRaw,
    DateTimeOffset? FormTimestampParsed,
    bool IsCurrent,
    DateTimeOffset ImportedAt);