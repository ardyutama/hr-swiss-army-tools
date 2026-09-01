using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Import;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class Import : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/candidates/import",
                async (
                    long vacancyId,
                    HttpRequest request,
                    ICommandHandler<ImportCandidatesCommand, ImportCandidatesResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    IFormFileCollection files;
                    try
                    {
                        files = (await request.ReadFormAsync(cancellationToken)).Files;
                    }
                    catch (BadHttpRequestException)
                    {
                        return CustomResults.Problem(InvalidFormData());
                    }
                    catch (InvalidDataException)
                    {
                        return CustomResults.Problem(InvalidFormData());
                    }

                    var commandFiles = files
                        .Select(file => new ImportCandidateFile(
                            file.FileName,
                            file.ContentType,
                            file.Length,
                            file.OpenReadStream()))
                        .ToList();
                    try
                    {
                        var result = await handler.Handle(
                            new ImportCandidatesCommand(vacancyId, commandFiles),
                            cancellationToken);
                        return result.Match<IResult>(
                            TypedResults.Ok,
                            CustomResults.Problem);
                    }
                    finally
                    {
                        foreach (var file in commandFiles)
                        {
                            await file.Content.DisposeAsync();
                        }
                    }
                })
            .WithTags(Tags.Candidates)
            .WithName("ImportCandidates")
            .DisableAntiforgery();
    }

    private static ValidationError InvalidFormData() => CandidateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["files"] = ["The uploaded form data is invalid."]
        });
}