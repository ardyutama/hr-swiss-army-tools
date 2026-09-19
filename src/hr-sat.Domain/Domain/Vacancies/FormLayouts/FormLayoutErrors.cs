using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies.FormLayouts;

public static class FormLayoutErrors
{
    public static Error SnapshotRequired(long vacancyId) => Error.Conflict(
        "FormLayouts.HeaderSnapshotRequired",
        $"Vacancy '{vacancyId}' must import a form CSV before its Form Layout can be created.");

    public static Error NotFound(long vacancyId) => Error.NotFound(
        "FormLayouts.NotFound",
        $"Vacancy '{vacancyId}' does not have a Form Layout.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("FormLayouts.Invalid", "The Form Layout is invalid.", errors);
}