using FluentValidation;

namespace hr_sat.Application.Features.Vacancies;

public sealed class CreateVacancyCommandValidator : AbstractValidator<CreateVacancyCommand>
{
    public CreateVacancyCommandValidator()
    {
        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .MaximumLength(200);
        RuleFor(command => command.OpenedOn)
            .NotEqual(default(DateOnly))
            .WithMessage("Opening Date is required.");
        RuleFor(command => command.NeededHires)
            .InclusiveBetween(1, 9999)
            .When(command => command.NeededHires.HasValue)
            .WithMessage("Needed Hires must be between 1 and 9999.");
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
