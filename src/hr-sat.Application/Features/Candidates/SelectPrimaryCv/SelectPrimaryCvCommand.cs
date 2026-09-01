using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Application.Features.Candidates.SelectPrimaryCv;

public sealed record SelectPrimaryCvCommand(
    long VacancyId,
    long CandidateId,
    long DocumentId) : ICommand<CandidateDetailsResponse>;
