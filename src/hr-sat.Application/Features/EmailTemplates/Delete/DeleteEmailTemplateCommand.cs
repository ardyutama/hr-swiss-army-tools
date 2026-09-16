using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailTemplates.Delete;

public sealed record DeleteEmailTemplateCommand(long VacancyId, string? Kind) : ICommand;