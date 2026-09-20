using hr_sat.Application.Features.Candidates.PriorApplications;

namespace hr_sat.Application.Features.Candidates.ImportForm;

internal sealed record FormImportPlan(
    IReadOnlyList<FormImportGroupPlan> Groups,
    int Created,
    int Updated,
    int SkippedOutdated,
    int PriorApplications)
{
    public static FormImportPlan Build(
        IReadOnlyList<ParsedFormRow> rows,
        IReadOnlyDictionary<string, CurrentFormResponseState> currentResponsesByKey,
        IReadOnlyDictionary<string, IReadOnlyList<PriorApplicationMatch>> priorApplicationsByKey)
    {
        var groups = new List<FormImportGroupPlan>();
        var created = 0;
        var updated = 0;
        var skippedOutdated = 0;
        var priorApplications = 0;

        foreach (var groupRows in GroupRows(rows))
        {
            var identityKey = groupRows[0].IdentityKey;
            if (identityKey is null)
            {
                groups.Add(new CreateFormCandidatePlan(InMutationOrder(groupRows)));
                created++;
                continue;
            }

            if (!currentResponsesByKey.TryGetValue(identityKey, out var current))
            {
                groups.Add(new CreateFormCandidatePlan(InMutationOrder(groupRows)));
                if (priorApplicationsByKey.ContainsKey(identityKey))
                {
                    priorApplications++;
                }

                created++;
                continue;
            }

            var freshRows = groupRows
                .Where(row => IsLater(row.FormTimestampParsed, current.FormTimestampParsed))
                .ToList();
            if (freshRows.Count == 0)
            {
                skippedOutdated += groupRows.Count;
                continue;
            }

            groups.Add(new UpdateFormCandidatePlan(identityKey, InMutationOrder(freshRows)));
            skippedOutdated += groupRows.Count - freshRows.Count;
            if (priorApplicationsByKey.TryGetValue(identityKey, out var matches) &&
                matches.Any(match => match.CandidateId != current.CandidateId))
            {
                priorApplications++;
            }

            updated++;
        }

        return new FormImportPlan(groups, created, updated, skippedOutdated, priorApplications);
    }

    private static IReadOnlyList<IReadOnlyList<ParsedFormRow>> GroupRows(
        IReadOnlyList<ParsedFormRow> rows)
    {
        var groupedRows = new List<IReadOnlyList<ParsedFormRow>>();
        var keyedRows = new Dictionary<string, List<ParsedFormRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.IdentityKey is null)
            {
                groupedRows.Add([row]);
                continue;
            }

            if (!keyedRows.TryGetValue(row.IdentityKey, out var group))
            {
                group = [];
                keyedRows.Add(row.IdentityKey, group);
                groupedRows.Add(group);
            }

            group.Add(row);
        }

        return groupedRows;
    }

    private static IReadOnlyList<ParsedFormRow> InMutationOrder(IReadOnlyList<ParsedFormRow> rows)
    {
        var winner = FindLatest(rows);
        return [.. rows.Where(row => row.RowPosition != winner.RowPosition), winner];
    }

    private static ParsedFormRow FindLatest(IReadOnlyList<ParsedFormRow> rows)
    {
        var latest = rows[0];
        foreach (var row in rows.Skip(1))
        {
            if (IsLater(row.FormTimestampParsed, latest.FormTimestampParsed))
            {
                latest = row;
            }
        }

        return latest;
    }

    private static bool IsLater(DateTimeOffset? incoming, DateTimeOffset? current)
    {
        if (incoming.HasValue && current.HasValue)
        {
            return incoming.Value >= current.Value;
        }

        return true;
    }
}

internal abstract record FormImportGroupPlan;

internal sealed record CreateFormCandidatePlan(
    IReadOnlyList<ParsedFormRow> RowsInMutationOrder) : FormImportGroupPlan;

internal sealed record UpdateFormCandidatePlan(
    string IdentityKey,
    IReadOnlyList<ParsedFormRow> RowsInMutationOrder) : FormImportGroupPlan;

internal sealed record CurrentFormResponseState(
    long CandidateId,
    DateTimeOffset? FormTimestampParsed);
