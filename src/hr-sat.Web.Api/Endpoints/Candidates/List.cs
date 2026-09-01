using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class List : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vacancies/{vacancyId:long}/candidates", async (
            long vacancyId,
            IQueryHandler<ListCandidatesQuery, IReadOnlyList<CandidateSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(
                new ListCandidatesQuery(vacancyId),
                cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Candidates)
        .WithName("ListCandidates");
    }
}