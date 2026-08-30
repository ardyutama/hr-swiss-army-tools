using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Candidates;

internal static class GetCvDocument
{
    public static async Task<Results<FileStreamHttpResult, NotFound>> HandleAsync(
        long vacancyId,
        long candidateId,
        long documentId,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.CvDocuments
            .AsNoTracking()
            .Where(item =>
                item.Id == documentId &&
                item.CandidateId == candidateId &&
                dbContext.Candidates.Any(candidate =>
                    candidate.Id == candidateId && candidate.VacancyId == vacancyId))
            .Select(item => new
            {
                item.StorageKey,
                item.OriginalFilename
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            var stream = await fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
            return TypedResults.File(
                stream,
                "application/pdf",
                document.OriginalFilename,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }
}