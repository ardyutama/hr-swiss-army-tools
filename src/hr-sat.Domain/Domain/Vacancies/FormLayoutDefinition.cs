namespace hr_sat.Domain.Vacancies;

public sealed record FormLayoutDefinition(
    IReadOnlyList<FormLayoutColumn>? Columns);