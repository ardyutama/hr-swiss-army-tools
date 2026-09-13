using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.IntakeRounds;

namespace hr_sat.Application.Features.IntakeRounds.Create;

public sealed record CreateIntakeRoundCommand(long VacancyId, string? Name)
    : ICommand<IntakeRoundResponse>;