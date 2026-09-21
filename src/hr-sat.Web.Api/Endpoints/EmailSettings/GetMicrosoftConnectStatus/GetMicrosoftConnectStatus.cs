using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.GetMicrosoftConnectStatus;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class GetMicrosoftConnectStatus : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/settings/smtp/microsoft-connect/status",
                async (
                    IQueryHandler<GetMicrosoftConnectStatusQuery, MicrosoftConnectStatusResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetMicrosoftConnectStatusQuery(),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("GetMicrosoftConnectStatus");
    }
}
