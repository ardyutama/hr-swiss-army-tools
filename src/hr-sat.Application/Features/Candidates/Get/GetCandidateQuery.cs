using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Application.Features.Candidates.Get;

public sealed record GetCandidateQuery(
    long VacancyId,
    long CandidateId) : IQuery<CandidateDetailsResponse>;
