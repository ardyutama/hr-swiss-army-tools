using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.ScreeningRules;

public sealed record ScreeningRuleSetResponse(
    long Id,
    long VacancyId,
    IReadOnlyList<ScreeningRuleResponse> Rules)
{
    public static ScreeningRuleSetResponse From(ScreeningRuleSet ruleSet) => new(
        ruleSet.Id,
        ruleSet.VacancyId,
        ruleSet.Rules
            .Select(rule => new ScreeningRuleResponse(
                rule.Ordinal,
                FormatOperator(rule.Operator),
                rule.Value))
            .ToArray());

    private static string FormatOperator(ScreeningOperator screeningOperator) =>
        screeningOperator switch
        {
            ScreeningOperator.Equals => "equals",
            ScreeningOperator.NotEquals => "not-equals",
            ScreeningOperator.IsEmpty => "is-empty",
            ScreeningOperator.NotEmpty => "not-empty",
            ScreeningOperator.Contains => "contains",
            _ => screeningOperator.ToString().ToLowerInvariant()
        };
}

public sealed record ScreeningRuleResponse(
    int Ordinal,
    string Operator,
    string? Value);