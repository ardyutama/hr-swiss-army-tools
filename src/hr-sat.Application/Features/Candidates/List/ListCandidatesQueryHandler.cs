using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.List;

internal sealed class ListCandidatesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<ListCandidatesQuery, IReadOnlyList<CandidateSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CandidateSummaryResponse>>> Handle(
        ListCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == query.VacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                CandidateErrors.NotFound(query.VacancyId));
        }

        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.VacancyId == query.VacancyId)
            .OrderBy(candidate => candidate.ImportedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new CandidateSummaryResponse(
                candidate.Id,
                candidate.FullName,
                candidate.ContactEmail,
                candidate.Notes,
                candidate.ReviewStatus.ToString().ToLowerInvariant(),
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject,
                candidate.SourceSentAt))
            .ToListAsync(cancellationToken);

        return candidates;
    }
}
