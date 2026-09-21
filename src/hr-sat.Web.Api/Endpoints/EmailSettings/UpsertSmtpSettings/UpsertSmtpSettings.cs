using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class UpsertSmtpSettings : IEndpoint
{
    public sealed class Request
    {
        public string? SignInMethod { get; init; }
        public string? Host { get; init; }
        public int? Port { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? FromAddress { get; init; }
        public string? FromName { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/settings/smtp",
                async (
                    Request request,
                    ICommandHandler<UpsertSmtpSettingsCommand, UpsertSmtpSettingsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpsertSmtpSettingsCommand(
                            request.SignInMethod,
                            request.Host,
                            request.Port,
                            request.Username,
                            request.Password,
                            request.FromAddress,
                            request.FromName),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("UpsertSmtpSettings");
    }
}
