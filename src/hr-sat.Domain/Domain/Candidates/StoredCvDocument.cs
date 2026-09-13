namespace hr_sat.Domain.Candidates;

internal sealed record StoredCvDocument(
    string OriginalFilename,
    string StorageKey,
    int Position,
    bool IsPrimary,
    long SizeBytes,
    byte[] Sha256);