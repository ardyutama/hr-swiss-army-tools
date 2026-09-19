using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.ScreeningRules;
using hr_sat.Application.Features.ScreeningRules.Get;

namespace hr_sat.Web.Api.Endpoints.ScreeningRules;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/screening-rules",
                async (
                    long vacancyId,
                    IQueryHandler<GetScreeningRulesQuery, ScreeningRuleSetResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetScreeningRulesQuery(vacancyId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.ScreeningRules)
            .WithName("GetScreeningRules");
    }
}