using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Abstractions.Data;

public sealed record CandidateListReadRow(
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
    bool ScreenedOut,
    IReadOnlyList<ScreeningRuleMatch> FiredRules);