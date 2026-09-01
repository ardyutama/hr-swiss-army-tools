using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

public sealed record UpdateVacancyCommand(
    long Id,
    string? Title,
    DateOnly OpenedOn,
    IReadOnlyList<string?>? Requirements) : ICommand<VacancyDetailsResponse>;
