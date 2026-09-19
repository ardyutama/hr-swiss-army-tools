using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Features.Candidates.ReviewQueue;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class ReviewQueue : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/review-queue",
                async (
                    long vacancyId,
                    long roundId,
                    IQueryHandler<
                        GetReviewQueueQuery,
                        IReadOnlyList<CandidateSummaryResponse>> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetReviewQueueQuery(vacancyId, roundId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("GetReviewQueue");
    }
}