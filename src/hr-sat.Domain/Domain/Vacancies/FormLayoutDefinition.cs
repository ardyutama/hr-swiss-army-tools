namespace hr_sat.Domain.Vacancies;

public sealed record FormLayoutDefinition(
    IReadOnlyList<string>? HeaderSnapshot,
    int? NameColumnOrdinal,
    int? ContactEmailColumnOrdinal,
    int? ContactPhoneColumnOrdinal,
    int? CvLinkColumnOrdinal);