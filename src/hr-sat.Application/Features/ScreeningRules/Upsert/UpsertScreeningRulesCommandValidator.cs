using FluentValidation;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.ScreeningRules.Upsert;

public sealed class UpsertScreeningRulesCommandValidator
    : AbstractValidator<UpsertScreeningRulesCommand>
{
    public UpsertScreeningRulesCommandValidator()
    {
        RuleFor(command => command.VacancyId)
            .GreaterThan(0);
        RuleFor(command => command.Rules)
            .NotNull()
            .WithMessage("Screening Rules are required.");
        RuleFor(command => command.Rules)
            .Must(rules => rules is null || rules.Count <= 5)
            .WithMessage("A vacancy can have at most 5 Screening Rules.");
        RuleForEach(command => command.Rules)
            .ChildRules(rule =>
            {
                rule.RuleFor(item => item.Ordinal)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Screening Rule ordinals cannot be negative.");
                rule.RuleFor(item => item.Operator)
                    .IsInEnum()
                    .WithMessage("The Screening Rule operator is invalid.");
                rule.RuleFor(item => item.Value)
                    .NotEmpty()
                    .When(item => item.Operator is ScreeningOperator.Equals or
                        ScreeningOperator.NotEquals or
                        ScreeningOperator.Contains)
                    .WithMessage("This Screening Rule operator requires a non-empty value.");
                rule.RuleFor(item => item.Value)
                    .Null()
                    .When(item => item.Operator is ScreeningOperator.IsEmpty or
                        ScreeningOperator.NotEmpty)
                    .WithMessage("Empty Screening Rule operators cannot have a value.");
            });
    }
}