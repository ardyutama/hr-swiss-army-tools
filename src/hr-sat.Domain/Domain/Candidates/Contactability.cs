using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Candidates;

// The single implementation of the Contactable Candidate rule (CONTEXT.md): a Rejected
// Candidate or a Bench member with a contact email recorded. Screening is a separate
// concern — callers exclude Screened Out candidates before classification (ADR-0019).
public static class Contactability
{
    public static ContactabilityEvaluation Evaluate(
        CandidateReviewStatus reviewStatus,
        CandidateHireOutcome hireOutcome,
        string? contactEmail)
    {
        if (reviewStatus is CandidateReviewStatus.New or CandidateReviewStatus.Flagged)
        {
            return ContactabilityEvaluation.Undecided;
        }

        if (reviewStatus == CandidateReviewStatus.Shortlisted &&
            hireOutcome != CandidateHireOutcome.None)
        {
            return ContactabilityEvaluation.OutcomeRecorded;
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            return ContactabilityEvaluation.MissingEmail;
        }

        return ContactabilityEvaluation.Contactable(
            reviewStatus == CandidateReviewStatus.Shortlisted
                ? EmailTemplateKind.Shortlisted
                : EmailTemplateKind.Rejected);
    }
}
