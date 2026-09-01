namespace hr_sat.Application.Features.Candidates.GetCvDocument;

public sealed record CvDocumentDownloadResponse(
    Stream Content,
    string ContentType,
    string FileName);
