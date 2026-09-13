using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailTemplates.Upsert;

public sealed record UpsertEmailTemplateCommand(
    long VacancyId,
    string? Kind,
    string? Subject,
    string? Body)
    : ICommand<EmailTemplateResponse>;