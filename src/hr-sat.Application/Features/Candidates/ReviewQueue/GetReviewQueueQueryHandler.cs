using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.ReviewQueue;

internal sealed class GetReviewQueueQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetReviewQueueQuery, IReadOnlyList<CandidateSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CandidateSummaryResponse>>> Handle(
        GetReviewQueueQuery query,
        CancellationToken cancellationToken)
    {
        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == query.VacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                VacancyErrors.NotFound(query.VacancyId));
        }

        var round = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(item => item.Id == query.RoundId && item.VacancyId == query.VacancyId)
            .Select(item => new { item.ClosedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                IntakeRoundErrors.NotFound(query.RoundId));
        }

        var context = await ScreeningContextLoader.LoadAsync(
            query.VacancyId,
            round.ClosedAt is not null,
            dbContext,
            cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.IntakeRoundId == query.RoundId)
            .Include(candidate => candidate.FormResponses)
            .Include(candidate => candidate.CvDocuments)
            .OrderBy(candidate => candidate.SourceSentAt == null)
            .ThenBy(candidate => candidate.SourceSentAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        var items = candidates
            .Select(candidate => CandidateSummaryMapper.Map(candidate, context))
            .Where(candidate => !candidate.ScreenedOut)
            .ToArray();

        return Result<IReadOnlyList<CandidateSummaryResponse>>.Success(items);
    }
}