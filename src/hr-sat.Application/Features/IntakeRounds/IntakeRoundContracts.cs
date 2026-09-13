using hr_sat.Domain.IntakeRounds;

namespace hr_sat.Application.Features.IntakeRounds;

public sealed record IntakeRoundResponse(
    long Id,
    long VacancyId,
    int RoundNumber,
    string? Name,
    string Status,
    DateTimeOffset? ClosedAt,
    int CandidateCount)
{
    public static IntakeRoundResponse From(IntakeRound round, int candidateCount) => new(
        round.Id,
        round.VacancyId,
        round.RoundNumber,
        round.Name,
        round.IsOpen ? "open" : "closed",
        round.ClosedAt,
        candidateCount);
}