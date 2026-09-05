using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

public sealed record CreateVacancyCommand(
    string? Title,
    DateOnly OpenedOn,
    IReadOnlyList<string?>? Requirements) : ICommand<VacancyDetailsResponse>;
