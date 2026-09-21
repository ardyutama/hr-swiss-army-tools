using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.BeginMicrosoftConnect;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class BeginMicrosoftConnect : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/settings/smtp/microsoft-connect/begin",
                async (
                    ICommandHandler<BeginMicrosoftConnectCommand, MicrosoftConnectChallengeResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new BeginMicrosoftConnectCommand(),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("BeginMicrosoftConnect");
    }
}
