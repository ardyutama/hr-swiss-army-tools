using FluentValidation.Results;
using hr_sat.Application.Features.ScreeningRules.Preview;
using hr_sat.Application.Features.ScreeningRules.Upsert;
using hr_sat.Domain.Vacancies;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class ScreeningRulesValidatorTests
{
    [Fact]
    public void Validate_Should_RejectNonPositiveVacancyIdAndMissingRules_ForBothCommands() // domain: Screening Rules are vacancy-owned
    {
        foreach (var result in ValidateBoth(0, null))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.PropertyName)
                .ShouldContain(nameof(UpsertScreeningRulesCommand.VacancyId));
            result.Errors.Select(error => error.PropertyName)
                .ShouldContain(nameof(UpsertScreeningRulesCommand.Rules));
        }
    }

    [Fact]
    public void Validate_Should_RejectMoreThanFiveRules_ForBothCommands() // domain: a vacancy can have at most 5 Screening Rules
    {
        var rules = Enumerable.Range(0, 6)
            .Select(index => new ScreeningRule(index, ScreeningOperator.Equals, $"value-{index}"))
            .ToArray();

        foreach (var result in ValidateBoth(1, rules))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.ErrorMessage)
                .ShouldContain("A vacancy can have at most 5 Screening Rules.");
        }
    }

    [Fact]
    public void Validate_Should_RejectNegativeOrdinals_ForBothCommands() // domain: Screening Rules reference form columns by ordinal
    {
        foreach (var result in ValidateBoth(
            1,
            [new ScreeningRule(-1, ScreeningOperator.Equals, "value")]))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.ErrorMessage)
                .ShouldContain("Screening Rule ordinals cannot be negative.");
        }
    }

    [Fact]
    public void Validate_Should_RejectUnknownOperators_ForBothCommands() // domain: Screening Rules use the defined text operators
    {
        foreach (var result in ValidateBoth(
            1,
            [new ScreeningRule(1, (ScreeningOperator)999, "value")]))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.PropertyName)
                .ShouldContain("Rules[0].Operator");
        }
    }

    [Theory]
    [InlineData(ScreeningOperator.Equals)]
    [InlineData(ScreeningOperator.NotEquals)]
    [InlineData(ScreeningOperator.Contains)]
    public void Validate_Should_RequireValuesForValueOperators_ForBothCommands( // domain: Screening Rules are text conditions
        ScreeningOperator screeningOperator)
    {
        foreach (var result in ValidateBoth(
            1,
            [new ScreeningRule(1, screeningOperator, " ")]))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.ErrorMessage)
                .ShouldContain("This Screening Rule operator requires a non-empty value.");
        }
    }

    [Theory]
    [InlineData(ScreeningOperator.IsEmpty)]
    [InlineData(ScreeningOperator.NotEmpty)]
    public void Validate_Should_RejectValuesForEmptyOperators_ForBothCommands( // domain: is-empty and not-empty rules have no value
        ScreeningOperator screeningOperator)
    {
        foreach (var result in ValidateBoth(
            1,
            [new ScreeningRule(1, screeningOperator, "unexpected")]))
        {
            result.IsValid.ShouldBeFalse();
            result.Errors.Select(error => error.ErrorMessage)
                .ShouldContain("Empty Screening Rule operators cannot have a value.");
        }
    }

    [Fact]
    public void Validate_Should_AcceptACompleteRuleSet_ForBothCommands() // domain: Screening Rules support the five defined operators
    {
        var rules = new[]
        {
            new ScreeningRule(1, ScreeningOperator.Equals, "yes"),
            new ScreeningRule(2, ScreeningOperator.NotEquals, "no"),
            new ScreeningRule(3, ScreeningOperator.Contains, "text"),
            new ScreeningRule(4, ScreeningOperator.IsEmpty, null),
            new ScreeningRule(5, ScreeningOperator.NotEmpty, null)
        };

        foreach (var result in ValidateBoth(1, rules))
        {
            result.IsValid.ShouldBeTrue();
        }
    }

    private static IEnumerable<ValidationResult> ValidateBoth(
        long vacancyId,
        IReadOnlyList<ScreeningRule>? rules)
    {
        yield return new UpsertScreeningRulesCommandValidator().Validate(
            new UpsertScreeningRulesCommand(vacancyId, rules));
        yield return new PreviewScreeningRulesQueryValidator().Validate(
            new PreviewScreeningRulesQuery(vacancyId, rules));
    }
}