using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateDetails;

public sealed record UpdateCandidateDetailsCommand(
    long VacancyId,
    long CandidateId,
    string? FullName,
    string? ContactEmail) : ICommand<CandidateDetailsResponse>;
