using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

// The single server-side implementation of the Contactable Candidate rule (ADR-0019,
// decision 16) — mirrors the client classifier kind-for-kind.
public sealed class ContactabilityTests
{
    [Theory]
    [InlineData(CandidateReviewStatus.New)]
    [InlineData(CandidateReviewStatus.Flagged)]
    public void Evaluate_Should_ReturnUndecided_WhenReviewIsPending(
        CandidateReviewStatus reviewStatus) // domain: Contactable Candidate
    {
        Contactability.Evaluate(reviewStatus, CandidateHireOutcome.None, "person@example.com")
            .ShouldBe(ContactabilityEvaluation.Undecided);
    }

    [Theory]
    [InlineData(CandidateHireOutcome.Hired)]
    [InlineData(CandidateHireOutcome.Runaway)]
    [InlineData(CandidateHireOutcome.Declined)]
    public void Evaluate_Should_ReturnOutcomeRecorded_WhenShortlistedCandidateHasAnOutcome(
        CandidateHireOutcome outcome) // domain: Contactable Candidate
    {
        Contactability.Evaluate(CandidateReviewStatus.Shortlisted, outcome, "person@example.com")
            .ShouldBe(ContactabilityEvaluation.OutcomeRecorded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_Should_ReturnMissingEmail_WhenDecidedCandidateHasNoContactEmail(
        string? contactEmail) // domain: Contactable Candidate
    {
        Contactability.Evaluate(CandidateReviewStatus.Rejected, CandidateHireOutcome.None, contactEmail)
            .ShouldBe(ContactabilityEvaluation.MissingEmail);
    }

    [Fact]
    public void Evaluate_Should_ReturnContactableShortlisted_WhenShortlistedCandidateHasAnEmail() // domain: Contactable Candidate
    {
        Contactability.Evaluate(
                CandidateReviewStatus.Shortlisted,
                CandidateHireOutcome.None,
                "person@example.com")
            .ShouldBe(ContactabilityEvaluation.Contactable(EmailTemplateKind.Shortlisted));
    }

    [Fact]
    public void Evaluate_Should_ReturnContactableRejected_WhenRejectedCandidateHasAnEmail() // domain: Contactable Candidate
    {
        Contactability.Evaluate(
                CandidateReviewStatus.Rejected,
                CandidateHireOutcome.None,
                "person@example.com")
            .ShouldBe(ContactabilityEvaluation.Contactable(EmailTemplateKind.Rejected));
    }

    [Fact]
    public void Evaluate_Should_StayContactable_WhenRejectedCandidateHasAnOutcome() // domain: Contactable Candidate — the outcome block applies to shortlisted candidates only
    {
        Contactability.Evaluate(
                CandidateReviewStatus.Rejected,
                CandidateHireOutcome.Hired,
                "person@example.com")
            .ShouldBe(ContactabilityEvaluation.Contactable(EmailTemplateKind.Rejected));
    }
}
