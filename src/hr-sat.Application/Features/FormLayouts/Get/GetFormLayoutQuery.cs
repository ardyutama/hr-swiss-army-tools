using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.FormLayouts.Get;

public sealed record GetFormLayoutQuery(long VacancyId) : IQuery<FormLayoutResponse>;