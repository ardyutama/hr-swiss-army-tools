using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.Delete;

public sealed record DeleteCandidateCommand(long VacancyId, long CandidateId) : ICommand;
