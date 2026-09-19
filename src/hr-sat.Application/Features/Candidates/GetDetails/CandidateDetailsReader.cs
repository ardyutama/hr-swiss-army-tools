using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Features.Candidates;
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

        var priorApplications = await dbContext.Candidates
            .AsNoTracking()
            .Join(
                dbContext.IntakeRounds.AsNoTracking(),
                priorCandidate => priorCandidate.IntakeRoundId,
                priorRound => priorRound.Id,
                (priorCandidate, priorRound) => new
                {
                    Candidate = priorCandidate,
                    Round = priorRound
                })
            .Where(prior =>
                prior.Round.VacancyId == vacancyId &&
                prior.Candidate.Id != candidateId &&
                prior.Candidate.SourceSenderEmail != null &&
                prior.Candidate.SourceSenderEmail!.Trim().ToLower() == normalizedSenderEmail)
            .OrderByDescending(prior => prior.Round.RoundNumber)
            .ThenByDescending(prior => prior.Candidate.ImportedAt)
            .ThenByDescending(prior => prior.Candidate.Id)
            .Select(prior => new
            {
                prior.Round.RoundNumber,
                RoundName = prior.Round.Name,
                ReviewStatus = prior.Candidate.ReviewStatus.ToString().ToLowerInvariant(),
                prior.Candidate.ImportedAt,
                CandidateId = prior.Candidate.Id
            })
            .ToListAsync(cancellationToken);

        var formPriorApplications = await dbContext.CandidateFormResponses
            .AsNoTracking()
            .Join(
                dbContext.Candidates.AsNoTracking(),
                response => response.CandidateId,
                priorCandidate => priorCandidate.Id,
                (response, priorCandidate) => new
                {
                    Response = response,
                    Candidate = priorCandidate
                })
            .Join(
                dbContext.IntakeRounds.AsNoTracking(),
                prior => prior.Candidate.IntakeRoundId,
                priorRound => priorRound.Id,
                (prior, priorRound) => new
                {
                    prior.Response,
                    prior.Candidate,
                    Round = priorRound
                })
            .Where(prior =>
                prior.Round.VacancyId == vacancyId &&
                prior.Candidate.Id != candidateId &&
                prior.Response.IsCurrent &&
                prior.Response.IdentityKey == normalizedSenderEmail)
            .OrderByDescending(prior => prior.Round.RoundNumber)
            .ThenByDescending(prior => prior.Candidate.ImportedAt)
            .ThenByDescending(prior => prior.Candidate.Id)
            .Select(prior => new
            {
                prior.Round.RoundNumber,
                RoundName = prior.Round.Name,
                ReviewStatus = prior.Candidate.ReviewStatus.ToString().ToLowerInvariant(),
                prior.Candidate.ImportedAt,
                CandidateId = prior.Candidate.Id
            })
            .ToListAsync(cancellationToken);

        var allPriorApplications = priorApplications
            .Concat(formPriorApplications)
            .OrderByDescending(prior => prior.RoundNumber)
            .ThenByDescending(prior => prior.ImportedAt)
            .ThenByDescending(prior => prior.CandidateId)
            .ToList();

        return candidateWithScreening with
        {
            PriorApplications = allPriorApplications
                .GroupBy(prior => prior.RoundNumber)
                .Select(group => group.First())
                .Select(prior => new CandidatePriorApplicationResponse(
                    prior.RoundNumber,
                    prior.RoundName,
                    prior.ReviewStatus))
                .ToList()
        };
    }
}
