using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Get;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class GetCandidate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}",
                async (
                    long vacancyId,
                    long candidateId,
                    IQueryHandler<GetCandidateQuery, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetCandidateQuery(vacancyId, candidateId),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("GetCandidate");
    }
}
