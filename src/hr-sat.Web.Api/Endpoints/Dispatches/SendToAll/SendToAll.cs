using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Dispatches;
using hr_sat.Application.Features.Dispatches.SendToAll;

namespace hr_sat.Web.Api.Endpoints.Dispatches;

internal sealed class SendToAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/dispatches",
                async (
                    long vacancyId,
                    long roundId,
                    ICommandHandler<SendToAllCommand, DispatchRunReportResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new SendToAllCommand(vacancyId, roundId),
                        cancellationToken);
                    return result.Match<IResult>(
                        report => TypedResults.Created(uri: (string?)null, report),
                        CustomResults.Problem);
                })
            .WithTags(Tags.Dispatches)
            .WithName("SendToAll");
    }
}
