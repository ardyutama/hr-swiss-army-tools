using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.IntakeRounds;
using hr_sat.Application.Features.IntakeRounds.Create;

namespace hr_sat.Web.Api.Endpoints.IntakeRounds;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string? Name { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/rounds",
                async (
                    long vacancyId,
                    Request request,
                    ICommandHandler<CreateIntakeRoundCommand, IntakeRoundResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new CreateIntakeRoundCommand(vacancyId, request.Name),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.IntakeRounds)
            .WithName("CreateIntakeRound");
    }
}