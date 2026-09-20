using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.MessagingSummary;

public sealed record GetMessagingSummaryQuery(long VacancyId, long RoundId)
    : IQuery<IReadOnlyList<CandidateSummaryResponse>>;
