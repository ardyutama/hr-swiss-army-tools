using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.IntakeRounds;
using hr_sat.Application.Features.IntakeRounds.Close;

namespace hr_sat.Web.Api.Endpoints.IntakeRounds;

internal sealed class Close : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/close",
                async (
                    long vacancyId,
                    long roundId,
                    ICommandHandler<CloseIntakeRoundCommand, IntakeRoundResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new CloseIntakeRoundCommand(vacancyId, roundId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.IntakeRounds)
            .WithName("CloseIntakeRound");
    }
}