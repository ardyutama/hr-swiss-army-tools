using FluentValidation;

namespace hr_sat.Application.Features.Dispatches.SendToAll;

public sealed class SendToAllCommandValidator : AbstractValidator<SendToAllCommand>
{
    public SendToAllCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
    }
}
