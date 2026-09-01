namespace hr_sat.Application.Features.Candidates.Import;

public sealed record ImportCandidateFile(
    string? FileName,
    string? ContentType,
    long Length,
    Stream Content);
