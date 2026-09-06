using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.IntakeRounds;

namespace hr_sat.Application.Features.IntakeRounds.Close;

public sealed record CloseIntakeRoundCommand(long VacancyId, long RoundId)
    : ICommand<IntakeRoundResponse>;