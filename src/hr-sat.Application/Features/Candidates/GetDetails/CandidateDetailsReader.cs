using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.PriorApplications;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.GetDetails;

internal static class CandidateDetailsReader
{
    public static async Task<Result<CandidateDetailsResponse>> ReadAsync(
        long vacancyId,
        long roundId,
        long candidateId,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var round = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(item => item.Id == roundId && item.VacancyId == vacancyId)
            .Select(item => new { item.ClosedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(candidateId));
        }

        var candidate = await dbContext.Candidates
            .AsNoTracking()
            .Where(item => item.Id == candidateId && item.IntakeRoundId == roundId)
            .Include(item => item.FormResponses)
            .Include(item => item.CvDocuments)
            .Include(item => item.RequirementReviews)
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(candidateId));
        }

        var context = await ScreeningContextLoader.LoadAsync(
            vacancyId,
            round.ClosedAt is not null,
            dbContext,
            cancellationToken);
        var firedRules = candidate.EvaluateScreening(context);
        var candidateWithScreening = new CandidateDetailsResponse(
            candidate.Id,
            candidate.ReviewStatus.ToString().ToLowerInvariant(),
            candidate.HireOutcome.ToString().ToLowerInvariant(),
            candidate.PromotedFromRoundNumber,
            candidate.PromotedAt,
            Array.Empty<CandidatePriorApplicationResponse>(),
            candidate.FullName,
            candidate.ContactEmail,
            candidate.ContactPhone,
            candidate.Notes,
            candidate.RequirementReviews
                .OrderBy(review => review.VacancyRequirementId)
                .Select(review => new CandidateRequirementReviewResponse(
                    review.VacancyRequirementId,
                    review.Confirmed))
                .ToList(),
            candidate.SourceSenderName,
            candidate.SourceSenderEmail,
            candidate.SourceSubject,
            candidate.SourceBodyText,
            candidate.SourceSentAt,
            candidate.SourceOriginalFilename,
            candidate.CvDocuments
                .OrderBy(document => document.Position)
                .Select(document => new CandidateDocumentResponse(
                    document.Id,
                    document.OriginalFilename,
                    document.SizeBytes,
                    document.IsPrimary,
                    $"/api/vacancies/{vacancyId}/rounds/{roundId}/candidates/{candidateId}/cv-documents/{document.Id}"))
                .ToList(),
            candidate.IntakeSource.ToString().ToLowerInvariant(),
            candidate.IsResubmitted,
            candidate.FormResponses
                .OrderByDescending(response => response.IsCurrent)
                .ThenBy(response => response.ImportedAt)
                .Select(response => new CandidateFormResponseResponse(
                    response.Cells,
                    response.FormTimestampRaw,
                    response.FormTimestampParsed,
                    response.IsCurrent,
                    response.ImportedAt))
                .ToList(),
            CandidateScreeningResponse.From(
                firedRules.Count > 0,
                firedRules));

        var normalizedSenderEmail = candidateWithScreening.SourceSenderEmail is not null
            ? CandidateFormIdentity.NormalizeEmail(candidateWithScreening.SourceSenderEmail)
            : CandidateFormIdentity.NormalizeEmail(await dbContext.CandidateFormResponses
                .AsNoTracking()
                .Where(response =>
                    response.CandidateId == candidateId &&
                    response.IsCurrent &&
                    response.IdentityKey != null)
                .Select(response => response.IdentityKey)
                .SingleOrDefaultAsync(cancellationToken));
        if (normalizedSenderEmail is null)
        {
            return candidateWithScreening;
        }

        var priorApplicationsByKey = await PriorApplicationLookup.FindAsync(
            vacancyId,
            new HashSet<string>(StringComparer.Ordinal) { normalizedSenderEmail },
            dbContext,
            cancellationToken);

        return candidateWithScreening with
        {
            PriorApplications = priorApplicationsByKey.TryGetValue(
                normalizedSenderEmail,
                out var matches)
                ? matches
                    .Where(match => match.CandidateId != candidateId)
                    .GroupBy(match => match.RoundNumber)
                    .Select(group => group.First())
                    .Select(match => new CandidatePriorApplicationResponse(
                        match.RoundNumber,
                        match.RoundName,
                        match.ReviewStatus))
                    .ToList()
                : Array.Empty<CandidatePriorApplicationResponse>()
        };
    }
}
