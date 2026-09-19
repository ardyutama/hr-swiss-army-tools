using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies.FormLayouts;

public sealed record FormLayoutUpsertedDomainEvent(
    long VacancyId) : IDomainEvent;