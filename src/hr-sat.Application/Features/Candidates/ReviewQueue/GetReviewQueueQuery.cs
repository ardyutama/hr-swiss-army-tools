using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.ReviewQueue;

public sealed record GetReviewQueueQuery(long VacancyId, long RoundId)
    : IQuery<IReadOnlyList<CandidateSummaryResponse>>;