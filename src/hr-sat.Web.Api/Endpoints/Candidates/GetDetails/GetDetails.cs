using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.GetDetails;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class GetDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}",
                async (
                    long vacancyId,
                    long candidateId,
                    IQueryHandler<GetCandidateDetailsQuery, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetCandidateDetailsQuery(vacancyId, candidateId),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("GetCandidateDetails");
    }
}