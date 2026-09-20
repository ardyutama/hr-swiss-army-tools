using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Dispatches;
using hr_sat.Application.Features.Dispatches.RetryFailed;

namespace hr_sat.Web.Api.Endpoints.Dispatches;

internal sealed class RetryFailed : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/dispatches/{runId:long}/retry",
                async (
                    long vacancyId,
                    long roundId,
                    long runId,
                    ICommandHandler<RetryFailedDispatchesCommand, DispatchRunReportResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new RetryFailedDispatchesCommand(vacancyId, roundId, runId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.Dispatches)
            .WithName("RetryFailedDispatches");
    }
}
