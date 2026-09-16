namespace hr_sat.Application.Features.Candidates.ImportForm;

public sealed record ImportFormFile(
    string FileName,
    string? ContentType,
    long Length,
    Stream Content);