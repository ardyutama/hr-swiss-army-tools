using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailTemplates.Sources;

public sealed record GetTemplateSourcesQuery(long VacancyId, string? Kind)
    : IQuery<IReadOnlyList<EmailTemplateSourceResponse>>;