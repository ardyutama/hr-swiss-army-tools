using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.List;

public sealed record CandidateSummaryResponse(
    long Id,
    string? FullName,
    string? ContactEmail,
    string? Notes,
    string ReviewStatus,
    string HireOutcome,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    DateTimeOffset? SourceSentAt,
    int CvDocumentCount,
    string IntakeSource,
    bool IsResubmitted,
    string? CvLink,
    bool ScreenedOut,
    IReadOnlyList<FiredScreeningRuleResponse> FiredRules);
