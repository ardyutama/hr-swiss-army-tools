using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.Shared;

/// <summary>
/// Maps the read seam's row shape to the candidate summary contract. Shared by
/// the paged list and the unpaged summary consumers (review queue) — the second
/// same-reason consumer earned the extraction.
/// </summary>
internal static class CandidateListRowMapper
{
    public static CandidateSummaryResponse Map(CandidateListReadRow row)
    {
        // The row carries lowercase strings; the read seam doesn't load dispatch
        // data, so LastDispatch is null here (ADR-0019 scopes the indicator to
        // the messaging console).
        var contactability =
            EnumParsing.TryParseDefined<CandidateReviewStatus>(row.ReviewStatus, out var reviewStatus) &&
            EnumParsing.TryParseDefined<CandidateHireOutcome>(row.HireOutcome, out var hireOutcome)
                ? Contactability.Evaluate(reviewStatus, hireOutcome, row.ContactEmail).Kind
                : ContactabilityKind.Undecided; // unreachable: values are DB-backed enums

        return new(
            row.Id,
            row.FullName,
            row.ContactEmail,
            row.Notes,
            row.ReviewStatus,
            row.HireOutcome,
            row.SourceSenderName,
            row.SourceSenderEmail,
            row.SourceSubject,
            row.SourceSentAt,
            row.CvDocumentCount,
            row.IntakeSource,
            row.IsResubmitted,
            row.CvLink,
            row.ScreenedOut,
            CandidateScreeningResponse.From(row.ScreenedOut, row.FiredRules).FiredRules,
            contactability.ToApiValue(),
            LastDispatch: null);
    }
}
