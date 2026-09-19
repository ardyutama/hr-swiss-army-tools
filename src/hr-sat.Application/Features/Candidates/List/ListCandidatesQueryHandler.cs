using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.List;

internal sealed class ListCandidatesQueryHandler(
    IApplicationDbContext dbContext,
    ICandidateListReader? candidateListReader = null)
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
        if (candidateListReader is not null)
        {
            return await ReadFromInfrastructureAsync(
                query,
                round.ClosedAt is not null,
                includeScreenedOut,
                page,
                cancellationToken);
        }

        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.IntakeRoundId == query.RoundId)
            .Include(candidate => candidate.FormResponses)
            .Include(candidate => candidate.CvDocuments)
            .ToListAsync(cancellationToken);

        var rows = candidates
            .Select(candidate => ToRow(candidate, round.ClosedAt is not null, ruleSet, layout))
            .ToList();
        var nonScreenedRows = rows.Where(row => !row.Summary.ScreenedOut).ToList();
        var scopedRows = includeScreenedOut ? rows : nonScreenedRows;
        var filteredRows = scopedRows
            .Where(row => MatchesStatus(row.Summary, query.Status))
            .Where(row => MatchesOutcome(row.Summary, query.Outcome))
            .Where(row => MatchesQuery(row.Summary, query.Query))
            .ToList();
        var orderedRows = CandidateListRow.OrderByReceived(filteredRows, query.Sort).ToList();

        var items = orderedRows
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(row => row.Summary)
            .ToArray();

        return new CandidateListResponse(
            items,
            page,
            PageSize,
            scopedRows.Count,
            orderedRows.Count,
            new CandidateListCountsResponse(
                new CandidateStatusCountsResponse(
                    nonScreenedRows.Count(row => row.Summary.ReviewStatus == "new"),
                    nonScreenedRows.Count(row => row.Summary.ReviewStatus == "flagged"),
                    nonScreenedRows.Count(row => row.Summary.ReviewStatus == "shortlisted"),
                    nonScreenedRows.Count(row => row.Summary.ReviewStatus == "rejected")),
                new CandidateOutcomeCountsResponse(
                    nonScreenedRows.Count,
                    nonScreenedRows.Count(row =>
                        row.Summary.ReviewStatus == "shortlisted" &&
                        row.Summary.HireOutcome == "none"),
                    nonScreenedRows.Count(row =>
                        row.Summary.ReviewStatus == "shortlisted" &&
                        row.Summary.HireOutcome == "hired"),
                    nonScreenedRows.Count(row =>
                        row.Summary.ReviewStatus == "shortlisted" &&
                        row.Summary.HireOutcome == "runaway"),
                    nonScreenedRows.Count(row =>
                        row.Summary.ReviewStatus == "shortlisted" &&
                        row.Summary.HireOutcome == "declined")),
                rows.Count(row => row.Summary.ScreenedOut)));
    }

    private async Task<Result<CandidateListResponse>> ReadFromInfrastructureAsync(
        ListCandidatesQuery query,
        bool roundClosed,
        bool includeScreenedOut,
        int page,
        CancellationToken cancellationToken)
    {
        var readResult = await candidateListReader!.ReadAsync(
            new CandidateListReadRequest(
                query.RoundId,
                roundClosed,
                query.Status,
                query.Outcome,
                query.Query,
                query.Sort,
                includeScreenedOut,
                page),
            cancellationToken);
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == query.VacancyId, cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => readResult.CandidateIds.Contains(candidate.Id))
            .Include(candidate => candidate.FormResponses)
            .Include(candidate => candidate.CvDocuments)
            .ToListAsync(cancellationToken);
        var candidatesById = candidates.ToDictionary(candidate => candidate.Id);
        var items = readResult.CandidateIds
            .Where(candidatesById.ContainsKey)
            .Select(id => ToRow(
                candidatesById[id],
                roundClosed,
                ruleSet,
                layout).Summary)
            .ToArray();

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

    private static CandidateListRow ToRow(
        Candidate candidate,
        bool roundClosed,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout)
    {
        var screening = GetScreening(candidate, roundClosed, ruleSet, layout);
        return new CandidateListRow(
            new CandidateSummaryResponse(
                candidate.Id,
                candidate.FullName,
                candidate.ContactEmail,
                candidate.Notes,
                candidate.ReviewStatus.ToString().ToLowerInvariant(),
                candidate.HireOutcome.ToString().ToLowerInvariant(),
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject,
                candidate.SourceSentAt,
                candidate.CvDocuments.Count,
                candidate.IntakeSource.ToString().ToLowerInvariant(),
                candidate.IsResubmitted,
                screening.ScreenedOut,
                screening.FiredRules),
            candidate.SourceSentAt);
    }

    internal static CandidateScreeningResponse GetScreening(
        Candidate candidate,
        bool roundClosed,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout)
    {
        var firedRules = candidate.EvaluateScreening(roundClosed, ruleSet, layout);
        return CandidateScreeningResponse.From(firedRules.Count > 0, firedRules);
    }

    private static bool MatchesStatus(CandidateSummaryResponse candidate, string? status) =>
        string.IsNullOrWhiteSpace(status) ||
        string.Equals(status, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(candidate.ReviewStatus, status, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesOutcome(CandidateSummaryResponse candidate, string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome) ||
            string.Equals(outcome, "any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidate.ReviewStatus == "shortlisted" &&
            (string.Equals(outcome, "undecided", StringComparison.OrdinalIgnoreCase)
                ? candidate.HireOutcome == "none"
                : string.Equals(candidate.HireOutcome, outcome, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesQuery(CandidateSummaryResponse candidate, string? query)
    {
        var needle = query?.Trim();
        return string.IsNullOrEmpty(needle) ||
            new[]
            {
                candidate.FullName,
                candidate.ContactEmail,
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject
            }.Any(value => value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed record CandidateListRow(
        CandidateSummaryResponse Summary,
        DateTimeOffset? ReceivedAt)
    {
        public static IOrderedEnumerable<CandidateListRow> OrderByReceived(
            IEnumerable<CandidateListRow> rows,
            string? sort) =>
            string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)
                ? rows.OrderBy(row => row.ReceivedAt is null)
                    .ThenBy(row => row.ReceivedAt)
                    .ThenBy(row => row.Summary.Id)
                : rows.OrderBy(row => row.ReceivedAt is null)
                    .ThenByDescending(row => row.ReceivedAt)
                    .ThenBy(row => row.Summary.Id);
    }
}
