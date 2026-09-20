using hr_sat.Application.Features.Candidates.List;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Candidates.Shared;

internal static class CandidateSummaryMapper
{
    public static CandidateSummaryResponse Map(
        Candidate candidate,
        ScreeningContext context,
        CandidateLastDispatchResponse? lastDispatch = null)
    {
        var firedRules = candidate.EvaluateScreening(context);
        var screening = CandidateScreeningResponse.From(
            firedRules.Count > 0,
            firedRules);
        var contactability = Contactability.Evaluate(
            candidate.ReviewStatus,
            candidate.HireOutcome,
            candidate.ContactEmail);
        return new CandidateSummaryResponse(
            candidate.Id,
            candidate.FullName,
            candidate.ContactEmail,
            candidate.Notes,
            candidate.ReviewStatus.ToString().ToLowerInvariant(),
            candidate.HireOutcome.ToString().ToLowerInvariant(),
            candidate.SourceSenderName,
            candidate.SourceSenderEmail,
            candidate.SourceSubject,
            candidate.ReceivedAt,
            candidate.CvDocuments.Count,
            candidate.IntakeSource.ToString().ToLowerInvariant(),
            candidate.IsResubmitted,
            candidate.CvLink(context.Layout),
            screening.ScreenedOut,
            screening.FiredRules,
            contactability.Kind.ToApiValue(),
            lastDispatch);
    }
}