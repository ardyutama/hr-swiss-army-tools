namespace hr_sat.Application.Features.Candidates.List;

internal sealed record CandidateSummaryResponse(
    long Id,
    string? FullName,
    string? ContactEmail,
    string? Notes,
    string ReviewStatus,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    DateTimeOffset? SourceSentAt);
