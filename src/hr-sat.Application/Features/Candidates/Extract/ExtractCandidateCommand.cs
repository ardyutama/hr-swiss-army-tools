using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Application.Features.Candidates.Extract;

public sealed record ExtractCandidateCommand(
    long VacancyId,
    long CandidateId) : ICommand<CandidateDetailsResponse>;
