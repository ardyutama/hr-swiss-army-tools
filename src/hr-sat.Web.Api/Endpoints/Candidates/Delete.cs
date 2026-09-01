using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Delete;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}",
                async (
                    long vacancyId,
                    long candidateId,
                    ICommandHandler<DeleteCandidateCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new DeleteCandidateCommand(vacancyId, candidateId),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.NoContent,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("DeleteCandidate");
    }
}