namespace hr_sat.Domain.Vacancies.FormLayouts;

public sealed record FormLayoutColumn(
    int Ordinal,
    FormLayoutRole? Role,
    string? Label);
