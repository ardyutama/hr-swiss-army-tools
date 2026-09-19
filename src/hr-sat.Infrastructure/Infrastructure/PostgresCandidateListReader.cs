using System.Data;
using System.Data.Common;
using hr_sat.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace hr_sat.Infrastructure;

public sealed class PostgresCandidateListReader(AppDbContext dbContext)
    : ICandidateListReader
{
    private const int PageSize = 100;

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
            var candidateIds = await ReadCandidateIdsAsync(
                connection,
                candidateRows,
                filters,
                request,
                cancellationToken);

            return new CandidateListReadResult(
                candidateIds,
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

    private static async Task<IReadOnlyList<long>> ReadCandidateIdsAsync(
        DbConnection connection,
        string candidateRows,
        string filters,
        CandidateListReadRequest request,
        CancellationToken cancellationToken)
    {
        var scope = request.IncludeScreenedOut ? "TRUE" : "NOT screened_out";
        var direction = string.Equals(
            request.Sort,
            "oldest",
            StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";
        var offset = (long)(Math.Max(1, request.Page) - 1) * PageSize;
        var sql = $"""
            {candidateRows}
            SELECT id
            FROM candidate_rows
            WHERE {scope} AND {filters}
            ORDER BY
                CASE WHEN source_sent_at IS NULL THEN 1 ELSE 0 END,
                source_sent_at {direction},
                id
            LIMIT @page_size OFFSET @page_offset;
            """;
        await using var command = CreateCommand(connection, sql, request);
        AddFilterParameters(command, request);
        AddParameter(command, "page_size", NpgsqlDbType.Integer, PageSize);
        AddParameter(command, "page_offset", NpgsqlDbType.Bigint, offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidateIds = new List<long>();
        while (await reader.ReadAsync(cancellationToken))
        {
            candidateIds.Add(reader.GetInt64(0));
        }

        return candidateIds;
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
              "position(lower(@query) in lower(coalesce(source_sender_name, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(source_sender_email, ''))) > 0 OR " +
              "position(lower(@query) in lower(coalesce(source_subject, ''))) > 0";

        return $"({statusFilter}) AND ({outcomeFilter}) AND ({queryFilter})";
    }

    private static string BuildCandidateRows(bool roundClosed)
    {
        var screening = roundClosed
            ? "c.screened_out"
            : """
                c.intake_source = 'form'
                AND current_response.id IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(COALESCE(rule_set.rules, '[]'::jsonb)) AS screening_rule
                    WHERE CASE lower(COALESCE(
                        screening_rule ->> 'Operator',
                        screening_rule ->> 'operator'))
                        WHEN '0' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                            AND lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int)) = lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value')))
                        WHEN 'equals' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                            AND lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int)) = lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value')))
                        WHEN '1' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NULL
                            OR lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int)) <> lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value')))
                        WHEN 'notequals' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NULL
                            OR lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int)) <> lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value')))
                        WHEN '2' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NULL
                        WHEN 'isempty' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NULL
                        WHEN '3' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                        WHEN 'notempty' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                        WHEN '4' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                            AND position(lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value'))) in lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int))) > 0
                        WHEN 'contains' THEN
                            NULLIF(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int), '') IS NOT NULL
                            AND position(lower(btrim(COALESCE(
                                screening_rule ->> 'Value',
                                screening_rule ->> 'value'))) in lower(btrim(current_response.cells ->> (
                                COALESCE(
                                    screening_rule ->> 'Ordinal',
                                    screening_rule ->> 'ordinal'))::int))) > 0
                        ELSE FALSE
                    END
                )
                """;
        return $"""
            WITH candidate_rows AS (
                SELECT
                    c.id,
                    c.review_status,
                    c.hire_outcome,
                    c.full_name,
                    c.contact_email,
                    c.source_sender_name,
                    c.source_sender_email,
                    c.source_subject,
                    c.source_sent_at,
                    {screening} AS screened_out
                FROM candidate AS c
                LEFT JOIN candidate_form_response AS current_response
                    ON current_response.candidate_id = c.id
                    AND current_response.is_current
                LEFT JOIN intake_round AS round
                    ON round.id = c.intake_round_id
                LEFT JOIN screening_rule_set AS rule_set
                    ON rule_set.vacancy_id = round.vacancy_id
                WHERE c.intake_round_id = @round_id
            )
            """;
    }

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