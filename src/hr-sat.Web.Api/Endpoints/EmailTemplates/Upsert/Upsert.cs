using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailTemplates;
using hr_sat.Application.Features.EmailTemplates.Upsert;

namespace hr_sat.Web.Api.Endpoints.EmailTemplates;

internal sealed class Upsert : IEndpoint
{
    public sealed class Request
    {
        public string? Subject { get; init; }
        public string? Body { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/email-templates/{kind}",
                async (
                    long vacancyId,
                    string kind,
                    Request request,
                    ICommandHandler<UpsertEmailTemplateCommand, EmailTemplateResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpsertEmailTemplateCommand(
                            vacancyId,
                            kind,
                            request.Subject,
                            request.Body),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailTemplates)
            .WithName("UpsertEmailTemplate");
    }
}