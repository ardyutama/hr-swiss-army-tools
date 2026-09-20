using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.GetSmtpSettings;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class GetSmtpSettings : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/settings/smtp",
                async (
                    IQueryHandler<GetSmtpSettingsQuery, SmtpSettingsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetSmtpSettingsQuery(),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("GetSmtpSettings");
    }
}
