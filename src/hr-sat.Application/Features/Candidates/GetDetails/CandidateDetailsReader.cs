using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.GetDetails;

internal static class CandidateDetailsReader
{
    public static async Task<Result<CandidateDetailsResponse>> ReadAsync(
        long vacancyId,
        long candidateId,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate =>
                candidate.Id == candidateId &&
                candidate.VacancyId == vacancyId)
            .Select(candidate => new CandidateDetailsResponse(
                candidate.Id,
                candidate.ReviewStatus.ToString().ToLowerInvariant(),
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
                        $"/api/vacancies/{vacancyId}/candidates/{candidateId}/cv-documents/{document.Id}"))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return candidate ?? Result<CandidateDetailsResponse>.Failure(
            CandidateErrors.NotFound(candidateId));
    }
}
