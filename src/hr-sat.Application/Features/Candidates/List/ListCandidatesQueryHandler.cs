using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.List;

internal sealed class ListCandidatesQueryHandler(
    IApplicationDbContext dbContext,
    ICandidateListReader candidateListReader)
    : IQueryHandler<ListCandidatesQuery, CandidateListResponse>
{
    private const int PageSize = 100;

    public async Task<Result<CandidateListResponse>> Handle(
        ListCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == query.VacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return Result<CandidateListResponse>.Failure(
                CandidateErrors.NotFound(query.VacancyId));
        }

        var round = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(item => item.Id == query.RoundId && item.VacancyId == query.VacancyId)
            .Select(item => new { item.Id, item.ClosedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return Result<CandidateListResponse>.Failure(
                IntakeRoundErrors.NotFound(query.RoundId));
        }

        var page = Math.Max(1, query.Page);
        var includeScreenedOut = string.Equals(
            query.Screened,
            "all",
            StringComparison.OrdinalIgnoreCase);
        var readResult = await candidateListReader.ReadAsync(
            new CandidateListReadRequest(
                query.RoundId,
                round.ClosedAt is not null,
                query.Status,
                query.Outcome,
                query.Query,
                query.Sort,
                includeScreenedOut,
                page,
                PageSize),
            cancellationToken);
        var items = readResult.Rows.Select(ToResponse).ToArray();

        return new CandidateListResponse(
            items,
            page,
            PageSize,
            readResult.Total,
            readResult.FilteredTotal,
            new CandidateListCountsResponse(
                new CandidateStatusCountsResponse(
                    readResult.Counts.New,
                    readResult.Counts.Flagged,
                    readResult.Counts.Shortlisted,
                    readResult.Counts.Rejected),
                new CandidateOutcomeCountsResponse(
                    readResult.Counts.Any,
                    readResult.Counts.Undecided,
                    readResult.Counts.Hired,
                    readResult.Counts.Runaway,
                    readResult.Counts.Declined),
                readResult.Counts.ScreenedOut));
    }

    private static CandidateSummaryResponse ToResponse(CandidateListReadRow row) =>
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
