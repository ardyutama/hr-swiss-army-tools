using hr_sat.Domain;

namespace hr_sat.Domain.EmailTemplates;

public sealed record EmailTemplateDeletedDomainEvent(
    long VacancyId,
    EmailTemplateKind Kind) : IDomainEvent;