using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateOutcome;

public sealed record UpdateCandidateOutcomeCommand(
    long VacancyId,
    long RoundId,
    long CandidateId,
    string? Outcome) : ICommand<CandidateDetailsResponse>;