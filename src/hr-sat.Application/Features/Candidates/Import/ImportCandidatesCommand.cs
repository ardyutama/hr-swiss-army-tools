using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.Import;

public sealed record ImportCandidatesCommand(
    long VacancyId,
    long RoundId,
    IReadOnlyList<ImportCandidateFile>? Files) : ICommand<ImportCandidatesResponse>;
