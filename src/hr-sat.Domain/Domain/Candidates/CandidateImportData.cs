namespace hr_sat.Domain.Candidates;

internal sealed record CandidateImportData(
    long IntakeRoundId,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    string? SourceBodyText,
    DateTimeOffset? SourceSentAt,
    string SourceOriginalFilename,
    string SourceStorageKey,
    long SourceSizeBytes,
    byte[] SourceSha256,
    DateTimeOffset ImportedAt,
    IReadOnlyList<StoredCvDocument> CvDocuments);