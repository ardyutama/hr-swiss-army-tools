using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Application.Features.EmailTemplates;

public sealed record EmailTemplateResponse(
    long Id,
    long VacancyId,
    string Kind,
    string Subject,
    string Body)
{
    public static EmailTemplateResponse From(EmailTemplate template) => new(
        template.Id,
        template.VacancyId,
        template.Kind.ToApiValue(),
        template.Subject,
        template.Body);
}