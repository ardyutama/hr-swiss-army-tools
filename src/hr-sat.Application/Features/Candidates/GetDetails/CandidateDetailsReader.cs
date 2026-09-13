using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
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
        var candidate = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate =>
                candidate.Id == candidateId &&
                candidate.IntakeRoundId == roundId &&
                dbContext.IntakeRounds.Any(round =>
                    round.Id == roundId && round.VacancyId == vacancyId))
            .Select(candidate => new CandidateDetailsResponse(
                candidate.Id,
                candidate.ReviewStatus.ToString().ToLowerInvariant(),
                candidate.HireOutcome.ToString().ToLowerInvariant(),
                candidate.PromotedFromRoundNumber,
                candidate.PromotedAt,
                Array.Empty<CandidatePriorApplicationResponse>(),
                candidate.FullName,
                candidate.ContactEmail,
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
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(candidateId));
        }

        if (candidate.SourceSenderEmail is null)
        {
            return candidate;
        }

        var normalizedSenderEmail = candidate.SourceSenderEmail
            .Trim()
            .ToLowerInvariant();

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
                prior.Candidate.IntakeRoundId != roundId &&
                prior.Candidate.SourceSenderEmail != null &&
                prior.Candidate.SourceSenderEmail!.Trim().ToLower() ==
                normalizedSenderEmail)
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

        return candidate with
        {
            PriorApplications = priorApplications
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
