using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public sealed class ScreeningRuleSet : Entity
{
    private ScreeningRuleSet()
    {
    }

    private ScreeningRuleSet(
        long vacancyId,
        FormLayout layout,
        ScreeningRuleDefinition definition)
    {
        VacancyId = vacancyId;
        ReplaceValues(definition);
    }

    public long VacancyId { get; private set; }
    public IReadOnlyList<ScreeningRule> Rules { get; private set; } = [];

    internal static Result<ScreeningRuleSet> Create(
        long vacancyId,
        FormLayout layout,
        ScreeningRuleDefinition definition)
    {
        var validationResult = Validate(vacancyId, layout, definition);
        return validationResult.IsFailure
            ? Result<ScreeningRuleSet>.Failure(validationResult.Error)
            : new ScreeningRuleSet(vacancyId, layout, definition);
    }

    internal Result Replace(FormLayout layout, ScreeningRuleDefinition definition)
    {
        var validationResult = Validate(VacancyId, layout, definition);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        ReplaceValues(definition);
        return Result.Success();
    }

    public IReadOnlyList<int> Evaluate(IReadOnlyList<string?> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        return Rules
            .Select((rule, index) => Matches(rule, cells) ? index : -1)
            .Where(index => index >= 0)
            .ToArray();
    }

    public IReadOnlyList<ScreeningRuleMatch> EvaluateWithDisplay(
        IReadOnlyList<string?> cells,
        FormLayout layout)
    {
        return FormatDisplay(Evaluate(cells), layout);
    }

    public IReadOnlyList<ScreeningRuleMatch> FormatDisplay(
        IReadOnlyList<int> firedIndexes,
        FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(firedIndexes);
        ArgumentNullException.ThrowIfNull(layout);

        return firedIndexes
            .Select(index => new ScreeningRuleMatch(
                index,
                FormatDisplay(Rules[index], layout)))
            .ToArray();
    }

    private static bool Matches(
        ScreeningRule rule,
        IReadOnlyList<string?> cells)
    {
        var cell = rule.Ordinal < cells.Count ? cells[rule.Ordinal] : null;
        var isEmpty = string.IsNullOrWhiteSpace(cell);
        return rule.Operator switch
        {
            ScreeningOperator.Equals => !isEmpty && string.Equals(
                cell!.Trim(),
                rule.Value,
                StringComparison.InvariantCultureIgnoreCase),
            ScreeningOperator.NotEquals => isEmpty || !string.Equals(
                cell!.Trim(),
                rule.Value,
                StringComparison.InvariantCultureIgnoreCase),
            ScreeningOperator.IsEmpty => isEmpty,
            ScreeningOperator.NotEmpty => !isEmpty,
            ScreeningOperator.Contains => !isEmpty && cell!.Trim().Contains(
                rule.Value!,
                StringComparison.InvariantCultureIgnoreCase),
            _ => false
        };
    }

    private static Result Validate(
        long vacancyId,
        FormLayout layout,
        ScreeningRuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new Dictionary<string, string[]>();
        if (vacancyId <= 0)
        {
            errors[nameof(VacancyId)] = ["Vacancy is required."];
        }

        if (definition.Rules is null)
        {
            errors[nameof(ScreeningRuleDefinition.Rules)] =
                ["Screening Rules are required."];
        }
        else
        {
            if (definition.Rules.Count > 5)
            {
                errors[nameof(ScreeningRuleDefinition.Rules)] =
                    ["A vacancy can have at most 5 Screening Rules."];
            }

            for (var index = 0; index < definition.Rules.Count; index++)
            {
                var rule = definition.Rules[index];
                var prefix = $"rules[{index}]";
                if (rule.Ordinal < 0)
                {
                    errors[$"{prefix}.ordinal"] = ["A Screening Rule column ordinal cannot be negative."];
                }
                else if (rule.Ordinal >= layout.HeaderSnapshot.Length)
                {
                    errors[$"{prefix}.ordinal"] =
                        ["A Screening Rule column ordinal must exist in the form header snapshot."];
                }

                if (!Enum.IsDefined(rule.Operator))
                {
                    errors[$"{prefix}.operator"] = ["The Screening Rule operator is invalid."];
                    continue;
                }

                var requiresValue = rule.Operator is
                    ScreeningOperator.Equals or
                    ScreeningOperator.NotEquals or
                    ScreeningOperator.Contains;
                var hasValue = !string.IsNullOrWhiteSpace(rule.Value);
                if (requiresValue && !hasValue)
                {
                    errors[$"{prefix}.value"] =
                        ["This Screening Rule operator requires a non-empty value."];
                }
                else if (!requiresValue && rule.Value is not null)
                {
                    errors[$"{prefix}.value"] =
                        ["Empty Screening Rule operators cannot have a value."];
                }
            }
        }

        return errors.Count == 0 ? Result.Success() : ScreeningRuleSetErrors.Invalid(errors);
    }

    private static string FormatDisplay(ScreeningRule rule, FormLayout layout)
    {
        var column = layout.Columns.SingleOrDefault(item => item.Ordinal == rule.Ordinal);
        var columnName = string.IsNullOrWhiteSpace(column?.Label)
            ? rule.Ordinal < layout.HeaderSnapshot.Length
                ? Truncate(layout.HeaderSnapshot[rule.Ordinal])
                : $"Column {rule.Ordinal + 1}"
            : column.Label!.Trim();
        var operatorName = rule.Operator.ToString().ToLowerInvariant();
        return rule.Operator is ScreeningOperator.Equals or
            ScreeningOperator.NotEquals or
            ScreeningOperator.Contains
            ? $"{columnName} · {operatorName} \"{rule.Value}\""
            : $"{columnName} · {operatorName}";
    }

    private static string Truncate(string value)
    {
        const int maxLength = 24;
        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..(maxLength - 1)]}…";
    }

    private void ReplaceValues(ScreeningRuleDefinition definition)
    {
        Rules = definition.Rules!
            .Select(rule => rule with
            {
                Value = rule.Operator is ScreeningOperator.IsEmpty or ScreeningOperator.NotEmpty
                    ? null
                    : rule.Value!.Trim()
            })
            .ToArray();
    }
}