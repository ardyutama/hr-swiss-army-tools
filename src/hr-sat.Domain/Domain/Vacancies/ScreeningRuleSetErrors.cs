using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public static class ScreeningRuleSetErrors
{
    public static Error SnapshotRequired(long vacancyId) => Error.Conflict(
        "ScreeningRules.HeaderSnapshotRequired",
        $"Vacancy '{vacancyId}' must import form responses before Screening Rules can be created.");

    public static Error NotFound(long vacancyId) => Error.NotFound(
        "ScreeningRules.NotFound",
        $"Vacancy '{vacancyId}' does not have Screening Rules.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("ScreeningRules.Invalid", "The Screening Rules are invalid.", errors);
}