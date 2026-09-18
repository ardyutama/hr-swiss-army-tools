using FluentValidation;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

public sealed class UpsertFormLayoutCommandValidator
    : AbstractValidator<UpsertFormLayoutCommand>
{
    public UpsertFormLayoutCommandValidator()
    {
        RuleFor(command => command.VacancyId)
            .GreaterThan(0);
        RuleFor(command => command.Columns)
            .NotNull()
            .WithMessage("Form Layout columns are required.");
        RuleForEach(command => command.Columns)
            .ChildRules(column =>
            {
                column.RuleFor(item => item.Ordinal)
                    .GreaterThan(0)
                    .WithMessage("Column ordinals must be greater than zero because Timestamp is reserved.");
                column.RuleFor(item => item.Label)
                    .MaximumLength(40)
                    .WithMessage("Column labels must be 40 characters or fewer.");
                column.RuleFor(item => item.Label)
                    .Must(label => label is null || label == label.Trim())
                    .WithMessage("Column labels must be trimmed.");
                column.RuleFor(item => item.Label)
                    .Must(label => label is null ||
                        (!label.Contains('\r') && !label.Contains('\n')))
                    .WithMessage("Column labels must be single line.");
            });
    }
}