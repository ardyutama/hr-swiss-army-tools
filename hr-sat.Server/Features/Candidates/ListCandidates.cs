using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Candidates;

internal static class ListCandidates
{
    public static async Task<Results<Ok<IReadOnlyList<CandidateSummaryResponse>>, NotFound>> HandleAsync(
        long vacancyId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == vacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return TypedResults.NotFound();
        }

        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.VacancyId == vacancyId)
            .OrderBy(candidate => candidate.ImportedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.FullName,
                candidate.ContactEmail,
                candidate.Notes,
                candidate.ReviewStatus,
                candidate.ExtractionStatus,
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject,
                candidate.SourceSentAt
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<CandidateSummaryResponse>>(
            candidates
                .Select(candidate => new CandidateSummaryResponse(
                    candidate.Id,
                    candidate.FullName,
                    candidate.ContactEmail,
                    candidate.Notes,
                    candidate.ReviewStatus.ToString().ToLowerInvariant(),
                    candidate.ExtractionStatus.ToString().ToLowerInvariant(),
                    candidate.SourceSenderName,
                    candidate.SourceSenderEmail,
                    candidate.SourceSubject,
                    candidate.SourceSentAt))
                .ToList());
    }
}

internal sealed record CandidateSummaryResponse(
    long Id,
    string? FullName,
    string? ContactEmail,
    string? Notes,
    string ReviewStatus,
    string ExtractionStatus,
    string? SourceSenderName,
    string? SourceSenderEmail,
    string? SourceSubject,
    DateTimeOffset? SourceSentAt);