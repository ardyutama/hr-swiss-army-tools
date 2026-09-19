namespace hr_sat.Domain.Candidates.FormResponses;

public sealed record CandidateFormImportData(
    long IntakeRoundId,
    DateTimeOffset ImportedAt);