using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.ScreeningRules;
using hr_sat.Application.Features.ScreeningRules.Upsert;

namespace hr_sat.Web.Api.Endpoints.ScreeningRules;

internal sealed class Upsert : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/screening-rules",
                async (
                    long vacancyId,
                    UpsertRequest request,
                    ICommandHandler<UpsertScreeningRulesCommand, ScreeningRuleSetResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpsertScreeningRulesCommand(vacancyId, request.Rules),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.ScreeningRules)
            .WithName("UpsertScreeningRules");
    }
}