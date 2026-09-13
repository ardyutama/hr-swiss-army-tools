using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailTemplates.Render;

public sealed record RenderEmailTemplateQuery(
    long VacancyId,
    long CandidateId,
    string? Subject,
    string? Body)
    : IQuery<RenderedEmailTemplateResponse>;