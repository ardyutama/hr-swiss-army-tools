using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public static class VacancyErrors
{
    public static Error NotFound(long id) => Error.NotFound(
        "Vacancies.NotFound",
        $"Vacancy with id '{id}' was not found.");

    public static Error Closed(long id) => Error.Conflict(
        "Vacancies.Closed",
        $"Vacancy with id '{id}' is closed and cannot record hire outcomes.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("Vacancies.Invalid", "The vacancy is invalid.", errors);
}