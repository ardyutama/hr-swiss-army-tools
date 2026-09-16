using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailTemplates;
using hr_sat.Application.Features.EmailTemplates.Render;

namespace hr_sat.Web.Api.Endpoints.EmailTemplates;

internal sealed class Render : IEndpoint
{
    public sealed class Request
    {
        public string? Subject { get; init; }
        public string? Body { get; init; }
        public long CandidateId { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/email-templates/render",
                async (
                    long vacancyId,
                    Request request,
                    IQueryHandler<RenderEmailTemplateQuery, RenderedEmailTemplateResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new RenderEmailTemplateQuery(
                            vacancyId,
                            request.CandidateId,
                            request.Subject,
                            request.Body),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailTemplates)
            .WithName("RenderEmailTemplate");
    }
}