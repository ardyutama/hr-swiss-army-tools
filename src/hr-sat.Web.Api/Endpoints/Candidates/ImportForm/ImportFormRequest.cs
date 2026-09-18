using System.Text.Json;
using System.Text.Json.Serialization;
using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.Extensions.Primitives;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class ImportFormRequest : IAsyncDisposable
{
    private ImportFormRequest(
        ImportFormFile file,
        FormLayoutDefinition? layout,
        bool confirmDrift)
    {
        File = file;
        Layout = layout;
        ConfirmDrift = confirmDrift;
    }

    public ImportFormFile File { get; }
    public FormLayoutDefinition? Layout { get; }
    public bool ConfirmDrift { get; }

    public static async Task<Result<ImportFormRequest>> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        IFormFileCollection formFiles;
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
            formFiles = form.Files;
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
        var layoutResult = ReadLayout(form["layout"]);
        if (layoutResult.IsFailure)
        {
            return Result<ImportFormRequest>.Failure(layoutResult.Error);
        }

        var confirmDriftResult = ReadConfirmDrift(form["confirmDrift"]);
        if (confirmDriftResult.IsFailure)
        {
            return Result<ImportFormRequest>.Failure(confirmDriftResult.Error);
        }

        return new ImportFormRequest(new ImportFormFile(
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream()),
            layoutResult.Value,
            confirmDriftResult.Value);
    }

    public ValueTask DisposeAsync() => File.Content.DisposeAsync();

    private static ValidationError InvalidFormData() => CandidateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["file"] = ["The uploaded form data is invalid."]
        });

    private static Result<FormLayoutDefinition?> ReadLayout(StringValues value)
    {
        var json = value.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return (FormLayoutDefinition?)null;
        }

        try
        {
            var layout = JsonSerializer.Deserialize<FormLayoutDefinition>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters =
                    {
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                    }
                });
            return layout is null
                ? Result<FormLayoutDefinition?>.Failure(InvalidLayout())
                : layout;
        }
        catch (JsonException)
        {
            return Result<FormLayoutDefinition?>.Failure(InvalidLayout());
        }
    }

    private static Result<bool> ReadConfirmDrift(StringValues value)
    {
        var rawValue = value.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return bool.TryParse(rawValue, out var confirmDrift)
            ? confirmDrift
            : Result<bool>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["confirmDrift"] = ["The confirmDrift field must be true or false."]
                }));
    }

    private static ValidationError InvalidLayout() => CandidateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["layout"] = ["The layout field must contain valid JSON Form Layout columns."]
        });
}