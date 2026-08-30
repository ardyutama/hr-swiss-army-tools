namespace hr_sat.Server.Domain.Vacancies;

public sealed class VacancyValidationException(
    IReadOnlyDictionary<string, string[]> errors) : Exception("The vacancy is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}