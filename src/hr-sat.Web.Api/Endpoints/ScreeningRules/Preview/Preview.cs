using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.ScreeningRules;
using hr_sat.Application.Features.ScreeningRules.Preview;

namespace hr_sat.Web.Api.Endpoints.ScreeningRules;

internal sealed class Preview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/screening-rules/preview",
                async (
                    long vacancyId,
                    PreviewRequest request,
                    IQueryHandler<PreviewScreeningRulesQuery, ScreeningRulesPreviewResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new PreviewScreeningRulesQuery(vacancyId, request.Rules),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.ScreeningRules)
            .WithName("PreviewScreeningRules");
    }
}