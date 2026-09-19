using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class List : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/candidates", async (
            long vacancyId,
            long roundId,
            string? status,
            string? outcome,
            string? query,
            string? sort,
            string? screened,
            int? page,
            IQueryHandler<ListCandidatesQuery, CandidateListResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(
                new ListCandidatesQuery(
                    vacancyId,
                    roundId,
                    status,
                    outcome,
                    query,
                    sort,
                    screened,
                    page ?? 1),
                cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Candidates)
        .WithName("ListCandidates");
    }
}