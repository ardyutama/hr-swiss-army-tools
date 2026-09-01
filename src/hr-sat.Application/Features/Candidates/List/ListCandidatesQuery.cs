using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.List;

public sealed record ListCandidatesQuery(long VacancyId)
    : IQuery<IReadOnlyList<CandidateSummaryResponse>>;
