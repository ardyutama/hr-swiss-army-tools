namespace hr_sat.Domain.Vacancies;

public sealed record FormLayoutColumn(
    int Ordinal,
    FormLayoutRole? Role,
    string? Label);
