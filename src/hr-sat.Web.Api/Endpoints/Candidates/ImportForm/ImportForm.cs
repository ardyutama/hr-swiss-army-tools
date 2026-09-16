using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.ImportForm;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class ImportForm : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/candidates/import-form",
                async (
                    long vacancyId,
                    long roundId,
                    HttpRequest request,
                    ICommandHandler<ImportFormCommand, ImportFormResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var importRequest = await ImportFormRequest.ReadAsync(request, cancellationToken);
                    if (importRequest.IsFailure)
                    {
                        return CustomResults.Problem(importRequest.Error);
                    }

                    await using var input = importRequest.Value;
                    var result = await handler.Handle(
                        new ImportFormCommand(vacancyId, roundId, input.File),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("ImportForm")
            .DisableAntiforgery();
    }
}