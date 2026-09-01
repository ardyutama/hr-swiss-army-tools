using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Extract;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class Extract : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/extract",
                async (
                    long vacancyId,
                    long candidateId,
                    ICommandHandler<ExtractCandidateCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new ExtractCandidateCommand(vacancyId, candidateId),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("ExtractCandidate");
    }
}
