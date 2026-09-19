namespace hr_sat.Domain.Vacancies;

public sealed record ScreeningRuleDefinition(
    IReadOnlyList<ScreeningRule>? Rules);