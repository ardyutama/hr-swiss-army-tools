using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Application.Features.Candidates.Import;

public sealed record ImportFileResponse(
    string FileName,
    string Status,
    string? Error,
    CandidateDetailsResponse? Candidate);

public sealed record ImportCandidatesResponse(IReadOnlyList<ImportFileResponse> Results);