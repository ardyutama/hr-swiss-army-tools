using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.RemoveSmtpSettings;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class RemoveSmtpSettings : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/api/settings/smtp",
                async (
                    ICommandHandler<RemoveSmtpSettingsCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new RemoveSmtpSettingsCommand(),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.NoContent, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("RemoveSmtpSettings");
    }
}
