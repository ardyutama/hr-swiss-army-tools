using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Import;

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
                    var importRequest = await ImportRequest.ReadAsync(request, cancellationToken);
                    if (importRequest.IsFailure)
                    {
                        return CustomResults.Problem(importRequest.Error);
                    }

                    await using var input = importRequest.Value;
                    var result = await handler.Handle(
                        new ImportCandidatesCommand(vacancyId, input.Files),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("ImportCandidates")
            .DisableAntiforgery();
    }
}