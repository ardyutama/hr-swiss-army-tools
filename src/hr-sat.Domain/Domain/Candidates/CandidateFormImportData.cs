namespace hr_sat.Domain.Candidates;

public sealed record CandidateFormImportData(
    long IntakeRoundId,
    DateTimeOffset ImportedAt);