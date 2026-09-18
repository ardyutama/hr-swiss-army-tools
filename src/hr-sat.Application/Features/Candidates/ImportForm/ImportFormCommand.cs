using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Domain.Vacancies;

public sealed record ImportFormCommand(
    long VacancyId,
    long RoundId,
    ImportFormFile? File,
    FormLayoutDefinition? Layout = null,
    bool ConfirmDrift = false) : ICommand<ImportFormResponse>;