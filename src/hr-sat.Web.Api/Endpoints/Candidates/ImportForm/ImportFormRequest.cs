using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class ImportFormRequest : IAsyncDisposable
{
    private ImportFormRequest(ImportFormFile file)
    {
        File = file;
    }

    public ImportFormFile File { get; }

    public static async Task<Result<ImportFormRequest>> ReadAsync(
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
            return Result<ImportFormRequest>.Failure(InvalidFormData());
        }
        catch (InvalidDataException)
        {
            return Result<ImportFormRequest>.Failure(InvalidFormData());
        }

        if (formFiles.Count != 1)
        {
            return Result<ImportFormRequest>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["file"] = ["Exactly one .csv file is required."]
                }));
        }

        var file = formFiles[0];
        return new ImportFormRequest(new ImportFormFile(
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream()));
    }

    public ValueTask DisposeAsync() => File.Content.DisposeAsync();

    private static ValidationError InvalidFormData() => CandidateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["file"] = ["The uploaded form data is invalid."]
        });
}