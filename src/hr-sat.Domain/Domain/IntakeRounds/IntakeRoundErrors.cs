using hr_sat.Domain;

namespace hr_sat.Domain.IntakeRounds;

public static class IntakeRoundErrors
{
    public static Error NotFound(long id) => Error.NotFound(
        "IntakeRounds.NotFound",
        $"Intake round with id '{id}' was not found.");

    public static Error ActiveRoundExists(long vacancyId) => Error.Conflict(
        "IntakeRounds.ActiveRoundExists",
        $"Vacancy with id '{vacancyId}' already has an active intake round.");

    public static Error NoActiveRound(long vacancyId) => Error.Conflict(
        "IntakeRounds.NoActiveRound",
        $"Vacancy with id '{vacancyId}' has no active intake round.");

    public static Error Closed(long id) => Error.Conflict(
        "IntakeRounds.Closed",
        $"Intake round with id '{id}' is closed and read-only.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("IntakeRounds.Invalid", "The intake round is invalid.", errors);
}