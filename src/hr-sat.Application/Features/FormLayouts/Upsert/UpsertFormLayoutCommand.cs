using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain.Vacancies.FormLayouts;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

public sealed record UpsertFormLayoutCommand(
    long VacancyId,
    IReadOnlyList<FormLayoutColumn>? Columns)
    : ICommand<FormLayoutResponse>;