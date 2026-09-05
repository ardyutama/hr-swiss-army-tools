using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateNotes;

public sealed record UpdateCandidateNotesCommand(
    long VacancyId,
    long CandidateId,
    string? Notes) : ICommand<CandidateDetailsResponse>;
