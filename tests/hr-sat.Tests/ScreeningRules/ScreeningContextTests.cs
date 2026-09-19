using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Tests.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class ScreeningContextTests
{
    [Fact]
    public void Live_Should_RejectNullInputs() // a live context without both inputs is a programmer error
    {
        var ruleSet = CreateRuleSet();
        var layout = CreateLayout();

        Should.Throw<ArgumentNullException>(() => ScreeningContext.Live(null!, layout));
        Should.Throw<ArgumentNullException>(() => ScreeningContext.Live(ruleSet, null!));
    }

    [Fact]
    public void Of_Should_NormalizeMissingInputsToNone()
    {
        ScreeningContext.Of(null, null).Kind.ShouldBe(ScreeningContextKind.None);
        ScreeningContext.Of(CreateRuleSet(), null).Kind.ShouldBe(ScreeningContextKind.None);
        ScreeningContext.Of(null, CreateLayout()).Kind.ShouldBe(ScreeningContextKind.None);
    }

    [Fact]
    public void Of_Should_ReturnLive_WhenBothInputsArePresent()
    {
        ScreeningContext.Of(CreateRuleSet(), CreateLayout()).Kind.ShouldBe(ScreeningContextKind.Live);
    }

    [Fact]
    public void EvaluateScreening_Should_NeverScreenEmailSourcedCandidates() // domain: email-sourced candidates have no form columns and are never screened
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        var context = ScreeningContext.Live(CreateRuleSet(), CreateLayout());

        candidate.EvaluateScreening(context).ShouldBeEmpty();
    }

    [Fact]
    public void EvaluateScreening_Should_EvaluateLiveRules_WhenContextIsLive() // domain: active rounds evaluate Screening Rules live
    {
        var candidate = CreateFormCandidate("No");
        var context = ScreeningContext.Live(CreateRuleSet(), CreateLayout());

        var firedRules = candidate.EvaluateScreening(context);

        firedRules.ShouldHaveSingleItem().ShouldBe(new ScreeningRuleMatch(
            0,
            "Availability · equals \"No\""));
    }

    [Fact]
    public void EvaluateScreening_Should_ReadTheStoredVerdict_WhenContextIsFrozen() // domain: the Screening Verdict settles with the round
    {
        var candidate = CreateFormCandidate("No");
        candidate.FreezeScreening(ScreeningContext.Live(CreateRuleSet(), CreateLayout()));

        var firedRules = candidate.EvaluateScreening(ScreeningContext.Frozen);

        candidate.ScreenedOut.ShouldBeTrue();
        firedRules.ShouldHaveSingleItem().ShouldBe(new ScreeningRuleMatch(
            0,
            "Availability · equals \"No\""));
    }

    [Fact]
    public void EvaluateScreening_Should_ReturnEmpty_ForFrozenCandidateThatPassedScreening()
    {
        var candidate = CreateFormCandidate("Yes");
        candidate.FreezeScreening(ScreeningContext.Live(CreateRuleSet(), CreateLayout()));

        candidate.EvaluateScreening(ScreeningContext.Frozen).ShouldBeEmpty();
        candidate.ScreenedOut.ShouldBeFalse();
        candidate.ScreeningVerdict.ShouldBeEmpty();
    }

    [Fact]
    public void EvaluateScreening_Should_ReturnEmpty_WhenContextIsNone() // domain: a vacancy without Screening Rules screens nobody
    {
        var candidate = CreateFormCandidate("No");

        candidate.EvaluateScreening(ScreeningContext.None).ShouldBeEmpty();
    }

    [Fact]
    public void FreezeScreening_Should_Throw_WhenContextIsFrozen() // freezing from frozen is a programmer error
    {
        var candidate = CreateFormCandidate("No");

        Should.Throw<ArgumentException>(
            () => candidate.FreezeScreening(ScreeningContext.Frozen));
    }

    private static Candidate CreateFormCandidate(string availability)
    {
        var importedAt = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
        var candidateResult = Candidate.ImportForm(new CandidateFormImportData(1, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var responseResult = candidateResult.Value.AddFormResponse(
            new CandidateFormResponseData(
                ["2026-09-19T10:00:00Z", "Applicant", "person@example.com", availability],
                "2026-09-19T10:00:00Z",
                importedAt,
                "person@example.com",
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        return candidateResult.Value;
    }

    private static ScreeningRuleSet CreateRuleSet()
    {
        var result = ScreeningRuleSet.Create(
            1,
            CreateLayout(),
            new ScreeningRuleDefinition([new ScreeningRule(3, ScreeningOperator.Equals, "No")]));

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
