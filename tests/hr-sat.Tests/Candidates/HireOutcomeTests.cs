using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class HireOutcomeTests
{
    [Fact]
    public void SetHireOutcome_Should_AllowSettledTransitions_WhenCandidateIsShortlisted() // domain: Hire Outcome
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, null).IsSuccess.ShouldBeTrue();

        candidate.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Runaway).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.None).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Declined).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();
        candidate.HireOutcome.ShouldBe(CandidateHireOutcome.Hired);
    }

    [Fact]
    public void SetHireOutcome_Should_RejectNonShortlistedCandidate_WhenSettingAnOutcome() // domain: Hire Outcome is set only on shortlisted candidates
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        var result = candidate.SetHireOutcome(CandidateHireOutcome.Hired);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["reviewStatus"]
            .ShouldContain("A hire outcome can only be set for a shortlisted candidate.");
    }

    [Fact]
    public void SetHireOutcome_Should_RejectInvalidTransition_WhenOutcomeDoesNotFollowMatrix() // domain: Hire Outcome transition rules
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, null).IsSuccess.ShouldBeTrue();

        var runaway = candidate.SetHireOutcome(CandidateHireOutcome.Runaway);

        runaway.IsFailure.ShouldBeTrue();
        runaway.Error.ShouldBeOfType<ValidationError>()
            .Errors["hireOutcome"]
            .ShouldContain("The requested hire outcome transition is not allowed.");
    }

    [Fact]
    public void ApplyReview_Should_RejectStatusChanges_WhenCandidateIsHiredOrRunaway() // domain: Hire Outcome preserves the active-hire review marker
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, null).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();

        var result = candidate.ApplyReview(CandidateReviewStatus.Flagged, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["reviewStatus"]
            .ShouldContain("Review status cannot change while the hire outcome is hired or runaway.");
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.Shortlisted);
    }
}