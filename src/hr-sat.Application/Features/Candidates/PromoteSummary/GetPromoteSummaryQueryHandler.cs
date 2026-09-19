using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.PromoteSummary;

internal sealed class GetPromoteSummaryQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetPromoteSummaryQuery, IReadOnlyList<CandidateSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CandidateSummaryResponse>>> Handle(
        GetPromoteSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Where(item => item.Id == query.VacancyId)
            .Select(item => new { item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (vacancy is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                VacancyErrors.NotFound(query.VacancyId));
        }

        if (vacancy.Status == VacancyStatus.Closed)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                VacancyErrors.Closed(query.VacancyId));
        }

        var sourceRound = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round =>
                round.Id == query.SourceRoundId &&
                round.VacancyId == query.VacancyId)
            .Select(round => new { round.ClosedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceRound is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                IntakeRoundErrors.NotFound(query.SourceRoundId));
        }

        if (sourceRound.ClosedAt is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                IntakeRoundErrors.NotClosed(query.SourceRoundId));
        }

        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate =>
                candidate.IntakeRoundId == query.SourceRoundId &&
                candidate.HireOutcome == CandidateHireOutcome.None &&
                (candidate.ReviewStatus == CandidateReviewStatus.New ||
                 candidate.ReviewStatus == CandidateReviewStatus.Flagged ||
                 candidate.ReviewStatus == CandidateReviewStatus.Shortlisted))
            .Include(candidate => candidate.FormResponses)
            .Include(candidate => candidate.CvDocuments)
            .OrderBy(candidate => candidate.SourceSentAt == null)
            .ThenBy(candidate => candidate.SourceSentAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        var items = candidates
            .Select(candidate => CandidateSummaryMapper.Map(
                candidate,
                roundClosed: true,
                ruleSet,
                layout))
            .ToArray();

        return Result<IReadOnlyList<CandidateSummaryResponse>>.Success(items);
    }
}