using FluentValidation;

namespace hr_sat.Application.Features.Vacancies;

public sealed class UpdateVacancyCommandValidator : AbstractValidator<UpdateVacancyCommand>
{
    public UpdateVacancyCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .MaximumLength(200);
        RuleFor(command => command.OpenedOn)
            .NotEqual(default(DateOnly))
            .WithMessage("Opening Date is required.");
        RuleFor(command => command.Requirements)
            .NotEmpty()
            .WithMessage("At least one vacancy requirement is required.");
        RuleForEach(command => command.Requirements)
            .Cascade(CascadeMode.Stop)
            .Must(requirement => !string.IsNullOrWhiteSpace(requirement))
            .WithMessage("Each vacancy requirement is required.")
            .MaximumLength(200);
    }
}
