using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.UpdateNotes;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class UpdateNotes : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/candidates/{candidateId:long}/notes",
                async (
                    long vacancyId,
                    long roundId,
                    long candidateId,
                    UpdateNotesRequest request,
                    ICommandHandler<UpdateCandidateNotesCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpdateCandidateNotesCommand(vacancyId, roundId, candidateId, request.Notes),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("UpdateCandidateNotes");
    }
}
