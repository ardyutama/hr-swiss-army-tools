using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.ImportForm;

public sealed record ImportFormCommand(
    long VacancyId,
    long RoundId,
    ImportFormFile? File) : ICommand<ImportFormResponse>;