using FluentValidation;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

public sealed class UpsertFormLayoutCommandValidator
    : AbstractValidator<UpsertFormLayoutCommand>
{
    public UpsertFormLayoutCommandValidator()
    {
        RuleFor(command => command.VacancyId)
            .GreaterThan(0);
        RuleFor(command => command.HeaderSnapshot)
            .NotEmpty()
            .WithMessage("The form header snapshot must contain at least one column.");
    }
}