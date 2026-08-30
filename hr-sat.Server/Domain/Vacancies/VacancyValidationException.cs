using hr_sat.Server.Domain;

namespace hr_sat.Server.Domain.Vacancies;

public sealed class VacancyValidationException(
    IReadOnlyDictionary<string, string[]> errors)
    : DomainValidationException(errors, "The vacancy is invalid.");