using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.PromoteCandidates;

public sealed record PromoteCandidatesCommand(
    long VacancyId,
    long RoundId,
    long SourceRoundId,
    IReadOnlyList<long>? CandidateIds)
    : ICommand<IReadOnlyList<CandidateSummaryResponse>>;