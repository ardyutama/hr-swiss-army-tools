using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public sealed record ScreeningRuleSetUpsertedDomainEvent(
    long VacancyId) : IDomainEvent;