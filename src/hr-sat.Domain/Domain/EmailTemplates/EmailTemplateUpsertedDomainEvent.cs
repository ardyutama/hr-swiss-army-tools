using hr_sat.Domain;

namespace hr_sat.Domain.EmailTemplates;

public sealed record EmailTemplateUpsertedDomainEvent(
    long VacancyId,
    EmailTemplateKind Kind) : IDomainEvent;