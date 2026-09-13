using FluentValidation;

namespace hr_sat.Application.Features.IntakeRounds.Create;

public sealed class CreateIntakeRoundCommandValidator : AbstractValidator<CreateIntakeRoundCommand>
{
    public CreateIntakeRoundCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.Name)
            .MaximumLength(200)
            .WithMessage("Name must be 200 characters or fewer.");
    }
}