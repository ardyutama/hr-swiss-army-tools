using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.GetCvDocument;

internal sealed class GetCvDocumentQueryHandler(
    IApplicationDbContext dbContext,
    IPrivateFileStorage fileStorage)
    : IQueryHandler<GetCvDocumentQuery, CvDocumentDownloadResponse>
{
    public async Task<Result<CvDocumentDownloadResponse>> Handle(
        GetCvDocumentQuery query,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.CvDocuments
            .AsNoTracking()
            .Where(item =>
                item.Id == query.DocumentId &&
                item.CandidateId == query.CandidateId &&
                dbContext.Candidates.Any(candidate =>
                    candidate.Id == query.CandidateId && candidate.VacancyId == query.VacancyId))
            .Select(item => new
            {
                item.StorageKey,
                item.OriginalFilename
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result<CvDocumentDownloadResponse>.Failure(
                CandidateErrors.NotFound(query.DocumentId));
        }

        try
        {
            var stream = await fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
            return new CvDocumentDownloadResponse(stream, "application/pdf", document.OriginalFilename);
        }
        catch (FileNotFoundException)
        {
            return Result<CvDocumentDownloadResponse>.Failure(
                CandidateErrors.NotFound(query.DocumentId));
        }
        catch (DirectoryNotFoundException)
        {
            return Result<CvDocumentDownloadResponse>.Failure(
                CandidateErrors.NotFound(query.DocumentId));
        }
    }
}
