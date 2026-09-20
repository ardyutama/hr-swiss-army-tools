using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailSettings.TestSmtpConnection;

namespace hr_sat.Web.Api.Endpoints.EmailSettings;

internal sealed class TestSmtpConnection : IEndpoint
{
    public sealed class Request
    {
        public string? Host { get; init; }
        public int? Port { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/settings/smtp/test",
                async (
                    Request request,
                    ICommandHandler<TestSmtpConnectionCommand, TestSmtpConnectionResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new TestSmtpConnectionCommand(
                            request.Host,
                            request.Port,
                            request.Username,
                            request.Password),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailSettings)
            .WithName("TestSmtpConnection");
    }
}
