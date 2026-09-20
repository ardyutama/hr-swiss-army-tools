using System.Data;
using System.Data.Common;
using System.Text.Json;
using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace hr_sat.Infrastructure;

public sealed class PostgresCandidateListReader(AppDbContext dbContext)
    : ICandidateListReader
{
    public async Task<CandidateListReadResult> ReadAsync(
        CandidateListReadRequest request,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var candidateRows = BuildCandidateRows(request.RoundClosed);
            var filters = BuildFilters(request);
            var screeningContext = request.RoundClosed
                ? null
                : await ReadScreeningContextAsync(request.RoundId, cancellationToken);
            var counts = await ReadCountsAsync(
                connection,
                candidateRows,
                request,
                cancellationToken);
            var totals = await ReadTotalsAsync(
                connection,
                candidateRows,
                filters,
                request,
                cancellationToken);
            var rows = await ReadRowsAsync(
                connection,
                candidateRows,
                filters,
                request,
                screeningContext,
                cancellationToken);

            return new CandidateListReadResult(
                rows,
                totals.Total,
                totals.FilteredTotal,
                counts);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<ScreeningContext> ReadScreeningContextAsync(
        long roundId,
        CancellationToken cancellationToken)
    {
        var vacancyId = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(round => round.Id == roundId)
            .Select(round => round.VacancyId)
            .SingleAsync(cancellationToken);
        var layout = await dbContext.FormLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == vacancyId, cancellationToken);
        var ruleSet = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VacancyId == vacancyId, cancellationToken);
        return new ScreeningContext(ruleSet, layout);
    }

    private static async Task<CandidateListReadCounts> ReadCountsAsync(
        DbConnection connection,
        string candidateRows,
        CandidateListReadRequest request,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {candidateRows}
            SELECT
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'new'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'flagged'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'shortlisted'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'rejected'),
                COUNT(*) FILTER (WHERE NOT screened_out),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'shortlisted' AND hire_outcome = 'none'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'shortlisted' AND hire_outcome = 'hired'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'shortlisted' AND hire_outcome = 'runaway'),
                COUNT(*) FILTER (WHERE NOT screened_out AND review_status = 'shortlisted' AND hire_outcome = 'declined'),
                COUNT(*) FILTER (WHERE screened_out)
            FROM candidate_rows;
            """;
        await using var command = CreateCommand(connection, sql, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new CandidateListReadCounts(
            ReadCount(reader, 0),
            ReadCount(reader, 1),
            ReadCount(reader, 2),
            ReadCount(reader, 3),
            ReadCount(reader, 4),
            ReadCount(reader, 5),
            ReadCount(reader, 6),
            ReadCount(reader, 7),
            ReadCount(reader, 8),
            ReadCount(reader, 9));
    }

    private static async Task<(int Total, int FilteredTotal)> ReadTotalsAsync(
        DbConnection connection,
        string candidateRows,
        string filters,
        CandidateListReadRequest request,
        CancellationToken cancellationToken)
    {
        var scope = request.IncludeScreenedOut ? "TRUE" : "NOT screened_out";
        var sql = $"""
            {candidateRows}
            SELECT
                COUNT(*) FILTER (WHERE {scope}),
                COUNT(*) FILTER (WHERE {scope} AND {filters})
            FROM candidate_rows;
            """;
        await using var command = CreateCommand(connection, sql, request);
        AddFilterParameters(command, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (ReadCount(reader, 0), ReadCount(reader, 1));
    }

    private static async Task<IReadOnlyList<CandidateListReadRow>> ReadRowsAsync(
        DbConnection connection,
        string candidateRows,
        string filters,
        CandidateListReadRequest request,
        ScreeningContext? screeningContext,
        CancellationToken cancellationToken)
    {
        var scope = request.IncludeScreenedOut ? "TRUE" : "NOT screened_out";
        var direction = string.Equals(
            request.Sort,
            "oldest",
            StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";
        var offset = (long)(Math.Max(1, request.Page) - 1) * request.PageSize;
        var sql = $"""
            {candidateRows}
            SELECT
                id,
                full_name,
                contact_email,
                contact_phone,
                notes,
                review_status,
                hire_outcome,
                source_sender_name,
                source_sender_email,
                source_subject,
                received_at,
                cv_document_count,
                intake_source,
                is_resubmitted,
                cv_link,
                screened_out,
                fired_rules::text
            FROM candidate_rows
            WHERE {scope} AND {filters}
            ORDER BY
                CASE WHEN received_at IS NULL THEN 1 ELSE 0 END,
                received_at {direction},
                id
            LIMIT @page_size OFFSET @page_offset;
            """;
        await using var command = CreateCommand(connection, sql, request);
        AddFilterParameters(command, request);
        AddParameter(command, "page_size", NpgsqlDbType.Integer, request.PageSize);
        AddParameter(command, "page_offset", NpgsqlDbType.Bigint, offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CandidateListReadRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var firedRules = ParseFiredRules(
                reader.GetString(16),
                request.RoundClosed,
                screeningContext);
            rows.Add(new CandidateListReadRow(
                reader.GetInt64(0),
                ReadNullableString(reader, 1),
                ReadNullableString(reader, 2),
                ReadNullableString(reader, 3),
                ReadNullableString(reader, 4),
                reader.GetString(5),
                reader.GetString(6),
                ReadNullableString(reader, 7),
                ReadNullableString(reader, 8),
                ReadNullableString(reader, 9),
                ReadNullableDateTimeOffset(reader, 10),
                reader.GetInt32(11),
                reader.GetString(12),
                reader.GetBoolean(13),
                ReadNullableString(reader, 14),
                reader.GetBoolean(15),
                firedRules));
        }

        return rows;
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        string sql,
        CandidateListReadRequest request)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "round_id", NpgsqlDbType.Bigint, request.RoundId);
        return command;
    }

    private static void AddFilterParameters(
        DbCommand command,
        CandidateListReadRequest request)
    {
        var status = Normalize(request.Status);
        if (status is not null && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            AddParameter(command, "status", NpgsqlDbType.Text, status);
        }

        var outcome = Normalize(request.Outcome);
        if (outcome is not null && !string.Equals(outcome, "any", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(outcome, "undecided", StringComparison.OrdinalIgnoreCase))
        {
            AddParameter(command, "outcome", NpgsqlDbType.Text, outcome);
        }

        var query = Normalize(request.Query);
        if (query is not null)
        {
            AddParameter(command, "query", NpgsqlDbType.Text, query);
        }
    }

    private static string BuildFilters(CandidateListReadRequest request)
    {
        var status = Normalize(request.Status);
        var statusFilter = status is null ||
            string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            ? "TRUE"
            : "lower(review_status) = lower(@status)";

        var outcome = Normalize(request.Outcome);
        var outcomeFilter = outcome is null ||
            string.Equals(outcome, "any", StringComparison.OrdinalIgnoreCase)
            ? "TRUE"
            : string.Equals(outcome, "undecided", StringComparison.OrdinalIgnoreCase)
                ? "review_status = 'shortlisted' AND hire_outcome = 'none'"
                : "review_status = 'shortlisted' AND lower(hire_outcome) = lower(@outcome)";

        var query = Normalize(request.Query);
        var queryFilter = query is null
            ? "TRUE"
            : "position(lower(@query) in lower(coalesce(full_name, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(contact_email, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(contact_phone, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(source_sender_name, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(source_sender_email, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(source_subject, ''))) > 0";

        return $"({statusFilter}) AND ({outcomeFilter}) AND ({queryFilter})";
    }

    private static IReadOnlyList<ScreeningRuleMatch> ParseFiredRules(
        string json,
        bool roundClosed,
        ScreeningContext? screeningContext)
    {
        if (roundClosed)
        {
            return JsonSerializer.Deserialize<ScreeningRuleMatch[]>(json) ?? [];
        }

        var firedIndexes = JsonSerializer.Deserialize<int[]>(json) ?? [];
        return screeningContext?.RuleSet is null || screeningContext.Layout is null
            ? []
            : screeningContext.RuleSet.FormatDisplay(firedIndexes, screeningContext.Layout);
    }

    private static string? ReadNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTimeOffset? ReadNullableDateTimeOffset(
        DbDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            DateTimeOffset value => value,
            DateTime value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("The candidate source date has an invalid database type.")
        };
    }

    private static string BuildCandidateRows(bool roundClosed)
    {
        var ruleOrdinal = "(COALESCE(screening_rule.rule ->> 'Ordinal', screening_rule.rule ->> 'ordinal'))::int";
        var cell = $"current_response.cells ->> {ruleOrdinal}";
        var trimmedCell = $"btrim({cell})";
        var nonEmptyCell = $"NULLIF({trimmedCell}, '')";
        var ruleValue = "btrim(COALESCE(screening_rule.rule ->> 'Value', screening_rule.rule ->> 'value'))";
        var normalizedCell = $"lower({trimmedCell})";
        var normalizedValue = $"lower({ruleValue})";
        var ruleOperator = "lower(COALESCE(screening_rule.rule ->> 'Operator', screening_rule.rule ->> 'operator'))";
        var matchesRule = $"""
            CASE {ruleOperator}
                WHEN '0' THEN
                    {nonEmptyCell} IS NOT NULL AND {normalizedCell} = {normalizedValue}
                WHEN 'equals' THEN
                    {nonEmptyCell} IS NOT NULL AND {normalizedCell} = {normalizedValue}
                WHEN '1' THEN
                    {nonEmptyCell} IS NULL OR {normalizedCell} <> {normalizedValue}
                WHEN 'notequals' THEN
                    {nonEmptyCell} IS NULL OR {normalizedCell} <> {normalizedValue}
                WHEN 'not-equals' THEN
                    {nonEmptyCell} IS NULL OR {normalizedCell} <> {normalizedValue}
                WHEN '2' THEN
                    {nonEmptyCell} IS NULL
                WHEN 'isempty' THEN
                    {nonEmptyCell} IS NULL
                WHEN 'is-empty' THEN
                    {nonEmptyCell} IS NULL
                WHEN '3' THEN
                    {nonEmptyCell} IS NOT NULL
                WHEN 'notempty' THEN
                    {nonEmptyCell} IS NOT NULL
                WHEN 'not-empty' THEN
                    {nonEmptyCell} IS NOT NULL
                WHEN '4' THEN
                    {nonEmptyCell} IS NOT NULL AND position({normalizedValue} in {normalizedCell}) > 0
                WHEN 'contains' THEN
                    {nonEmptyCell} IS NOT NULL AND position({normalizedValue} in {normalizedCell}) > 0
                ELSE FALSE
            END
            """;
        var activeScreeningJoin = roundClosed
            ? string.Empty
            : $"""
                LEFT JOIN LATERAL (
                    SELECT COALESCE(
                        jsonb_agg(
                            (screening_rule.rule_index - 1)::int
                            ORDER BY screening_rule.rule_index)
                            FILTER (WHERE {matchesRule}),
                        '[]'::jsonb) AS fired_rule_indexes
                    FROM jsonb_array_elements(
                        COALESCE(rule_set.rules, '[]'::jsonb))
                        WITH ORDINALITY AS screening_rule(rule, rule_index)
                ) AS screening
                    ON c.intake_source = 'form'
                    AND current_response.id IS NOT NULL
                """;
        var screeningProjection = roundClosed
            ? """
                    c.screened_out,
                    CASE
                        WHEN c.screened_out
                            THEN COALESCE(c.screening_verdict, '[]'::jsonb)
                        ELSE '[]'::jsonb
                    END AS fired_rules
                """
            : """
                    jsonb_array_length(
                        COALESCE(screening.fired_rule_indexes, '[]'::jsonb)) > 0
                        AS screened_out,
                    COALESCE(screening.fired_rule_indexes, '[]'::jsonb) AS fired_rules
                """;

        return $"""
            WITH candidate_rows AS (
                SELECT
                    c.id,
                    c.full_name,
                    c.contact_email,
                    c.contact_phone,
                    c.notes,
                    c.review_status,
                    c.hire_outcome,
                    c.source_sender_name,
                    c.source_sender_email,
                    c.source_subject,
                    c.source_sent_at,
                    COALESCE(c.source_sent_at, current_response.form_timestamp_parsed)
                        AS received_at,
                    (
                        SELECT COUNT(*)::int
                        FROM cv_document AS cv_document
                        WHERE cv_document.candidate_id = c.id
                    ) AS cv_document_count,
                    c.intake_source,
                    c.is_resubmitted,
                    CASE
                        WHEN c.intake_source = 'form' AND current_response.id IS NOT NULL
                            THEN NULLIF(btrim(
                                current_response.cells ->> (
                                    SELECT (layout_column.column_value ->> 'Ordinal')::int
                                    FROM jsonb_array_elements(layout.columns)
                                        AS layout_column(column_value)
                                    WHERE COALESCE(
                                        layout_column.column_value ->> 'Role',
                                        layout_column.column_value ->> 'role') IN ('3', 'CvLink', 'cvlink')
                                    LIMIT 1)), '')
                        ELSE NULL
                    END AS cv_link,
                    {screeningProjection}
                FROM candidate AS c
                LEFT JOIN candidate_form_response AS current_response
                    ON current_response.candidate_id = c.id
                    AND current_response.is_current
                LEFT JOIN intake_round AS round
                    ON round.id = c.intake_round_id
                LEFT JOIN form_layout AS layout
                    ON layout.vacancy_id = round.vacancy_id
                LEFT JOIN screening_rule_set AS rule_set
                    ON rule_set.vacancy_id = round.vacancy_id
                {activeScreeningJoin}
                WHERE c.intake_round_id = @round_id
            )
            """;
    }

    private sealed record ScreeningContext(
        ScreeningRuleSet? RuleSet,
        FormLayout? Layout);

    private static int ReadCount(DbDataReader reader, int ordinal) =>
        checked((int)reader.GetInt64(ordinal));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddParameter(
        DbCommand command,
        string name,
        NpgsqlDbType type,
        object value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type)
        {
            Value = value
        });
    }
}