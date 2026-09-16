using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.FormLayouts;

public sealed record FormLayoutResponse(
    long Id,
    long VacancyId,
    IReadOnlyList<string> HeaderSnapshot,
    int? NameColumnOrdinal,
    int? ContactEmailColumnOrdinal,
    int? ContactPhoneColumnOrdinal,
    int? CvLinkColumnOrdinal,
    bool IsValid)
{
    public static FormLayoutResponse From(FormLayout layout) => new(
        layout.Id,
        layout.VacancyId,
        layout.HeaderSnapshot,
        layout.NameColumnOrdinal,
        layout.ContactEmailColumnOrdinal,
        layout.ContactPhoneColumnOrdinal,
        layout.CvLinkColumnOrdinal,
        layout.IsValid);
}