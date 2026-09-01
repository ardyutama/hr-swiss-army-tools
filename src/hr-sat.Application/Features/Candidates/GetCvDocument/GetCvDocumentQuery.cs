using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Candidates.GetCvDocument;

public sealed record GetCvDocumentQuery(
    long VacancyId,
    long CandidateId,
    long DocumentId) : IQuery<CvDocumentDownloadResponse>;
