namespace hr_sat.Application.Features.EmailTemplates;

public sealed record EmailTemplateSourceResponse(
    long VacancyId,
    string VacancyTitle,
    DateOnly OpenedOn,
    string Kind,
    string Subject,
    string Body);