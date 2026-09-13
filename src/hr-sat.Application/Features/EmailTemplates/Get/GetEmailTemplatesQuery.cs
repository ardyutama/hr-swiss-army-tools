using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailTemplates.Get;

public sealed record GetEmailTemplatesQuery(long VacancyId)
    : IQuery<IReadOnlyList<EmailTemplateResponse>>;