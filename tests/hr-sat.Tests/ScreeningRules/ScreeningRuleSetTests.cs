using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class ScreeningRuleSetTests
{
    [Theory]
    [InlineData(ScreeningOperator.Equals, "  TIDAK  ", "Tidak", true)]
    [InlineData(ScreeningOperator.NotEquals, "Ya", " tidak ", true)]
    [InlineData(ScreeningOperator.Contains, "  Bersedia ditempatkan  ", " ditempatkan ", true)]
    [InlineData(ScreeningOperator.Equals, "Ya", "Tidak", false)]
    public void Evaluate_Should_UseTrimmedCaseInsensitiveTextSemantics( // domain: Screening Rules are case-insensitive text conditions
        ScreeningOperator screeningOperator,
        string cell,
        string ruleValue,
        bool expectedMatch)
    {
        var ruleSet = CreateRuleSet(new ScreeningRule(1, screeningOperator, ruleValue));

        var firedRules = ruleSet.Evaluate(["Timestamp", cell]);

        firedRules.ShouldBe(expectedMatch ? [0] : []);
    }

    [Theory]
    [InlineData(ScreeningOperator.IsEmpty, null, true)]
    [InlineData(ScreeningOperator.IsEmpty, "   ", true)]
    [InlineData(ScreeningOperator.IsEmpty, "answer", false)]
    [InlineData(ScreeningOperator.NotEmpty, null, false)]
    [InlineData(ScreeningOperator.NotEmpty, "   ", false)]
    [InlineData(ScreeningOperator.NotEmpty, "answer", true)]
    public void Evaluate_Should_TreatMissingAndWhitespaceCellsAsEmpty( // domain: is-empty and not-empty inspect raw form answers as text
        ScreeningOperator screeningOperator,
        string? cell,
        bool expectedMatch)
    {
        var ruleSet = CreateRuleSet(new ScreeningRule(2, screeningOperator, null));

        var firedRules = ruleSet.Evaluate(["Timestamp", "Name"]);
        var populatedFiredRules = ruleSet.Evaluate(["Timestamp", "Name", cell!]);

        firedRules.ShouldBe(screeningOperator == ScreeningOperator.IsEmpty ? [0] : []);
        populatedFiredRules.ShouldBe(expectedMatch ? [0] : []);
    }

    [Fact]
    public void Evaluate_Should_ReturnEveryMatchingRuleIndexForAndCombination() // domain: Screening Rules are AND-combined
    {
        var ruleSet = CreateRuleSet(
            new ScreeningRule(1, ScreeningOperator.Contains, "tidak"),
            new ScreeningRule(1, ScreeningOperator.NotEquals, "ya"),
            new ScreeningRule(1, ScreeningOperator.Equals, "ya"));

        var firedRules = ruleSet.Evaluate(["Timestamp", " Tidak bersedia "]);

        firedRules.ShouldBe([0, 1]);
    }

    [Fact]
    public void EvaluateWithDisplay_Should_UseColumnLabelAndOperatorValue() // domain: Screened Out candidates show which Screening Rule fired
    {
        var ruleSet = CreateRuleSet(new ScreeningRule(3, ScreeningOperator.Equals, "Tidak"));

        var firedRules = ruleSet.EvaluateWithDisplay(
            ["Timestamp", "Name", "Email", " Tidak "],
            CreateLayout());

        firedRules.ShouldHaveSingleItem().ShouldBe(new ScreeningRuleMatch(
            0,
            "Availability · equals \"Tidak\""));
    }

    private static ScreeningRuleSet CreateRuleSet(params ScreeningRule[] rules)
    {
        var result = ScreeningRuleSet.Create(
            1,
            CreateLayout(),
            new ScreeningRuleDefinition(rules));

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static FormLayout CreateLayout()
    {
        var result = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email", "Availability"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, null, "Availability")
            ]));

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}