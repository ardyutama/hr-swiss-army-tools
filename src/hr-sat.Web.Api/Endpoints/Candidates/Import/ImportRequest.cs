using hr_sat.Application.Features.Candidates.Import;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class ImportRequest : IAsyncDisposable
{
    private readonly IReadOnlyList<ImportCandidateFile> files;

    private ImportRequest(IReadOnlyList<ImportCandidateFile> files)
    {
        this.files = files;
    }

    public IReadOnlyList<ImportCandidateFile> Files => files;

    public static async Task<Result<ImportRequest>> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        IFormFileCollection formFiles;
        try
        {
            formFiles = (await request.ReadFormAsync(cancellationToken)).Files;
        }
        catch (BadHttpRequestException)
        {
            return Result<ImportRequest>.Failure(InvalidFormData());
        }
        catch (InvalidDataException)
        {
            return Result<ImportRequest>.Failure(InvalidFormData());
        }

        var commandFiles = new List<ImportCandidateFile>(formFiles.Count);
        try
        {
            foreach (var file in formFiles)
            {
                commandFiles.Add(new ImportCandidateFile(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    file.OpenReadStream()));
            }
        }
        catch
        {
            foreach (var file in commandFiles)
            {
                await file.Content.DisposeAsync();
            }

            throw;
        }

        return new ImportRequest(commandFiles);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var file in files)
        {
            await file.Content.DisposeAsync();
        }
    }

    private static ValidationError InvalidFormData() => CandidateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["files"] = ["The uploaded form data is invalid."]
        });
}