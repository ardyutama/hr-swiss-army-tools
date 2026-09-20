using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Application.Features.Candidates.Shared;

/// <summary>
/// Maps the read seam's row shape to the candidate summary contract. Shared by
/// the paged list and the unpaged summary consumers (review queue) — the second
/// same-reason consumer earned the extraction.
/// </summary>
internal static class CandidateListRowMapper
{
    public static CandidateSummaryResponse Map(CandidateListReadRow row) =>
        new(
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
            row.ScreenedOut,
            CandidateScreeningResponse.From(row.ScreenedOut, row.FiredRules).FiredRules);
}
