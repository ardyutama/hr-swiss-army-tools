using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using hr_sat.Server.Infrastructure.Vacancies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Candidates.Import;

internal static class ImportCandidates
{
    public static async Task<Results<Ok<ImportCandidatesResponse>, NotFound, ValidationProblem>> HandleAsync(
        long id,
        HttpRequest request,
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        IFormFileCollection files;
        try
        {
            files = (await request.ReadFormAsync(cancellationToken)).Files;
        }
        catch (BadHttpRequestException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["The uploaded form data is invalid."]
            });
        }
        catch (InvalidDataException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["The uploaded form data is invalid."]
            });
        }

        if (files.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["At least one .eml file is required."]
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(id, cancellationToken);
        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            vacancy.EnsureCanReceiveCandidateImport();
        }
        catch (VacancyValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors);
        }

        var existingHashKeys = (await dbContext.Candidates
                .Where(candidate => candidate.VacancyId == id)
                .Select(candidate => candidate.SourceSha256)
                .ToListAsync(cancellationToken))
            .Select(Convert.ToHexString)
            .ToHashSet(StringComparer.Ordinal);
        var filePreparer = new ImportFilePreparer(
            id,
            existingHashKeys,
            dbContext,
            fileStorage,
            timeProvider);
        var outcomes = new List<ImportFileOutcome>(files.Count);

        try
        {
            foreach (var file in files)
            {
                outcomes.Add(await filePreparer.PrepareAsync(file, cancellationToken));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await filePreparer.DeleteStoredFilesAsync();
            throw;
        }

        return TypedResults.Ok(new ImportCandidatesResponse(
            outcomes
                .Select(outcome => outcome.ToResponse(id))
                .ToList()));
    }
}