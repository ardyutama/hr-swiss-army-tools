namespace hr_sat.Domain.Vacancies;

public sealed record ScreeningRule(
    int Ordinal,
    ScreeningOperator Operator,
    string? Value);