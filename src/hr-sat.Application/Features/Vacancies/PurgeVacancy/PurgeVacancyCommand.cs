using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Vacancies;

public sealed record PurgeVacancyCommand(long Id) : ICommand;
