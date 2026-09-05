using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.GetDetails;

public sealed record GetCandidateDetailsQuery(long VacancyId, long CandidateId)
    : IQuery<CandidateDetailsResponse>;