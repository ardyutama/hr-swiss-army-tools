using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.PromoteSummary;

public sealed record GetPromoteSummaryQuery(long VacancyId, long SourceRoundId)
    : IQuery<IReadOnlyList<CandidateSummaryResponse>>;