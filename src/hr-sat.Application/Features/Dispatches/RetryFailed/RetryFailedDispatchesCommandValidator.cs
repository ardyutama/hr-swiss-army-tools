using FluentValidation;

namespace hr_sat.Application.Features.Dispatches.RetryFailed;

public sealed class RetryFailedDispatchesCommandValidator
    : AbstractValidator<RetryFailedDispatchesCommand>
{
    public RetryFailedDispatchesCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
        RuleFor(command => command.RunId).GreaterThan(0);
    }
}
