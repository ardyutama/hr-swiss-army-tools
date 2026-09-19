namespace hr_sat.Domain.Vacancies.FormLayouts;

public sealed record FormLayoutDefinition(
    IReadOnlyList<FormLayoutColumn>? Columns);