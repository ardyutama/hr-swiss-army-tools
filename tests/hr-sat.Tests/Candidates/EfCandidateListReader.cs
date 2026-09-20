using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Tests.Candidates;

internal sealed class EfCandidateListReader(TestDbContext dbContext) : ICandidateListReader
{
    public async Task<CandidateListReadResult> ReadAsync(
        CandidateListReadRequest request,
        CancellationToken cancellationToken)
    {
        var round = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(item => item.Id == request.RoundId)
            .Select(item => new { item.VacancyId, item.ClosedAt })
            .SingleAsync(cancellationToken);
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == round.VacancyId, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == round.VacancyId, cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.IntakeRoundId == request.RoundId)
            .Include(candidate => candidate.FormResponses)
            .Include(candidate => candidate.CvDocuments)
            .ToListAsync(cancellationToken);

        var context = request.RoundClosed
            ? ScreeningContext.Frozen
            : ScreeningContext.Of(ruleSet, layout);
        var rows = candidates
            .Select(candidate => ToRow(candidate, context, layout))
            .ToList();
        var nonScreenedRows = rows.Where(row => !row.ScreenedOut).ToList();
        var scopedRows = request.IncludeScreenedOut ? rows : nonScreenedRows;
        var filteredRows = scopedRows
            .Where(row => MatchesStatus(row, request.Status))
            .Where(row => MatchesOutcome(row, request.Outcome))
            .Where(row => MatchesQuery(row, request.Query))
            .ToList();
        var orderedRows = OrderByReceived(filteredRows, request.Sort).ToList();
        var page = Math.Max(1, request.Page);

        return new CandidateListReadResult(
            orderedRows
                .Skip((page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToArray(),
            scopedRows.Count,
            orderedRows.Count,
            new CandidateListReadCounts(
                nonScreenedRows.Count(row => row.ReviewStatus == "new"),
                nonScreenedRows.Count(row => row.ReviewStatus == "flagged"),
                nonScreenedRows.Count(row => row.ReviewStatus == "shortlisted"),
                nonScreenedRows.Count(row => row.ReviewStatus == "rejected"),
                nonScreenedRows.Count,
                nonScreenedRows.Count(row =>
                    row.ReviewStatus == "shortlisted" && row.HireOutcome == "none"),
                nonScreenedRows.Count(row =>
                    row.ReviewStatus == "shortlisted" && row.HireOutcome == "hired"),
                nonScreenedRows.Count(row =>
                    row.ReviewStatus == "shortlisted" && row.HireOutcome == "runaway"),
                nonScreenedRows.Count(row =>
                    row.ReviewStatus == "shortlisted" && row.HireOutcome == "declined"),
                rows.Count(row => row.ScreenedOut)));
    }

    private static CandidateListReadRow ToRow(
        Candidate candidate,
        ScreeningContext context,
        FormLayout? layout)
    {
        var firedRules = candidate.EvaluateScreening(context);
        return new CandidateListReadRow(
            candidate.Id,
            candidate.FullName,
            candidate.ContactEmail,
            candidate.ContactPhone,
            candidate.Notes,
            candidate.ReviewStatus.ToString().ToLowerInvariant(),
            candidate.HireOutcome.ToString().ToLowerInvariant(),
            candidate.SourceSenderName,
            candidate.SourceSenderEmail,
            candidate.SourceSubject,
            candidate.ReceivedAt,
            candidate.CvDocuments.Count,
            candidate.IntakeSource.ToString().ToLowerInvariant(),
            candidate.IsResubmitted,
            candidate.CvLink(layout),
            firedRules.Count > 0,
            firedRules);
    }

    private static bool MatchesStatus(CandidateListReadRow candidate, string? status) =>
        string.IsNullOrWhiteSpace(status) ||
        string.Equals(status, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(candidate.ReviewStatus, status, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesOutcome(CandidateListReadRow candidate, string? outcome)
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

    private static bool MatchesQuery(CandidateListReadRow candidate, string? query)
    {
        var needle = query?.Trim();
        return string.IsNullOrEmpty(needle) ||
            new[]
            {
                candidate.FullName,
                candidate.ContactEmail,
                candidate.ContactPhone,
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject
            }.Any(value => value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IOrderedEnumerable<CandidateListReadRow> OrderByReceived(
        IEnumerable<CandidateListReadRow> rows,
        string? sort) =>
        string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderBy(row => row.SourceSentAt is null)
                .ThenBy(row => row.SourceSentAt)
                .ThenBy(row => row.Id)
            : rows.OrderBy(row => row.SourceSentAt is null)
                .ThenByDescending(row => row.SourceSentAt)
                .ThenBy(row => row.Id);
}