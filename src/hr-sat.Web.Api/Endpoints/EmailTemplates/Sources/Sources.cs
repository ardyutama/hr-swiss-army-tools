using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailTemplates;
using hr_sat.Application.Features.EmailTemplates.Sources;

namespace hr_sat.Web.Api.Endpoints.EmailTemplates;

internal sealed class Sources : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/email-templates/sources",
                async (
                    long vacancyId,
                    string? kind,
                    IQueryHandler<
                        GetTemplateSourcesQuery,
                        IReadOnlyList<EmailTemplateSourceResponse>> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetTemplateSourcesQuery(vacancyId, kind),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailTemplates)
            .WithName("GetEmailTemplateSources");
    }
}