using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateSummaryMapperTests
{
    private static readonly DateTimeOffset ParsedTimestamp =
        new(2026, 9, 12, 9, 58, 0, TimeSpan.Zero);

    [Fact]
    public void Map_Should_ResolveTheFormIdentityReceivedAtAndCvLink_WhenContextIsLive()
    {
        var candidate = CreateFormCandidate("  https://drive.example.com/cv  ");

        var summary = CandidateSummaryMapper.Map(candidate, LiveContext());

        summary.IntakeSource.ShouldBe("form");
        summary.SourceSentAt.ShouldBe(ParsedTimestamp);
        summary.CvLink.ShouldBe("https://drive.example.com/cv");
    }

    [Fact]
    public void Map_Should_CoalesceReceivedAtButLoseTheCvLink_WhenContextIsFrozen()
    {
        var candidate = CreateFormCandidate("https://drive.example.com/cv");

        // A frozen context carries no layout, so the CV link cannot resolve; the
        // coalesced received moment is layout-independent and survives.
        var summary = CandidateSummaryMapper.Map(candidate, ScreeningContext.Frozen);

        summary.SourceSentAt.ShouldBe(ParsedTimestamp);
        summary.CvLink.ShouldBeNull();
        summary.ScreenedOut.ShouldBeFalse();
    }

    [Fact]
    public void Map_Should_LeaveTheCvLinkNull_WhenCandidateIsEmailSourced()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        var summary = CandidateSummaryMapper.Map(candidate, LiveContext());

        summary.IntakeSource.ShouldBe("email");
        summary.SourceSentAt.ShouldBe(new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));
        summary.CvLink.ShouldBeNull();
    }

    private static ScreeningContext LiveContext()
    {
        var layout = CreateCvLinkLayout();
        var ruleSetResult = ScreeningRuleSet.Create(
            1,
            layout,
            new ScreeningRuleDefinition([]));
        ruleSetResult.IsSuccess.ShouldBeTrue();
        return ScreeningContext.Live(ruleSetResult.Value, layout);
    }

    private static FormLayout CreateCvLinkLayout()
    {
        var result = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email", "CV link"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, FormLayoutRole.CvLink, null)
            ]));
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static Candidate CreateFormCandidate(string cvLinkCell)
    {
        var importedAt = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
        var candidateResult = Candidate.ImportForm(new CandidateFormImportData(1, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var responseResult = candidateResult.Value.AddFormResponse(
            new CandidateFormResponseData(
                ["2026-09-12T09:58:00Z", "Applicant", "person@example.com", cvLinkCell],
                "2026-09-12T09:58:00Z",
                ParsedTimestamp,
                "person@example.com",
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        return candidateResult.Value;
    }
}
