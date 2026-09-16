using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

public sealed record UpsertFormLayoutCommand(
    long VacancyId,
    IReadOnlyList<string>? HeaderSnapshot,
    int? NameColumnOrdinal,
    int? ContactEmailColumnOrdinal,
    int? ContactPhoneColumnOrdinal,
    int? CvLinkColumnOrdinal)
    : ICommand<FormLayoutResponse>;