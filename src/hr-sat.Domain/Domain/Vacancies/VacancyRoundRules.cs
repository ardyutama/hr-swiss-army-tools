using hr_sat.Domain;
using hr_sat.Domain.IntakeRounds;

namespace hr_sat.Domain.Vacancies;

internal static class VacancyRoundRules
{
    public static Result EnsureCanCreateRound(VacancyStatus status, long vacancyId, IntakeRound? activeRound)
    {
        var openResult = VacancyLifecycleRules.EnsureOpen(
            status,
            "A closed vacancy must be reopened before an intake round can be created.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        return activeRound is not null
            ? IntakeRoundErrors.ActiveRoundExists(vacancyId)
            : Result.Success();
    }

    public static Result EnsureCanCloseRound(
        VacancyStatus status,
        IntakeRound? round,
        long roundId)
    {
        var openResult = VacancyLifecycleRules.EnsureOpen(
            status,
            "A closed vacancy must be reopened before an intake round can be closed.");
        return openResult.IsFailure
            ? openResult
            : EnsureFound(round, roundId);
    }

    public static Result<IntakeRound> EnsureOpenRound(
        IntakeRound? round,
        long roundId,
        bool requireActive,
        long? activeRoundId,
        long vacancyId)
    {
        var foundResult = EnsureFound(round, roundId);
        if (foundResult.IsFailure)
        {
            return Result<IntakeRound>.Failure(foundResult.Error);
        }

        if (!round!.IsOpen)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.Closed(round.Id));
        }

        if (requireActive && activeRoundId != round.Id)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.NoActiveRound(vacancyId));
        }

        return round;
    }

    public static int NextRoundNumber(IReadOnlyList<IntakeRound> rounds) =>
        rounds.Count == 0 ? 1 : rounds.Max(round => round.RoundNumber) + 1;

    private static Result EnsureFound(IntakeRound? round, long roundId) =>
        round is null ? IntakeRoundErrors.NotFound(roundId) : Result.Success();
}
