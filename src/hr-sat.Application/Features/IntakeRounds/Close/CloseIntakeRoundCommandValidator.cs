using FluentValidation;

namespace hr_sat.Application.Features.IntakeRounds.Close;

public sealed class CloseIntakeRoundCommandValidator : AbstractValidator<CloseIntakeRoundCommand>
{
    public CloseIntakeRoundCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
    }
}