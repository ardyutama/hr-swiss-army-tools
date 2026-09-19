using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Features.Candidates.PromoteSummary;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class PromoteSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/rounds/{sourceRoundId:long}/promote-summary",
                async (
                    long vacancyId,
                    long sourceRoundId,
                    IQueryHandler<
                        GetPromoteSummaryQuery,
                        IReadOnlyList<CandidateSummaryResponse>> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetPromoteSummaryQuery(vacancyId, sourceRoundId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("GetPromoteSummary");
    }
}