using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.UpdateOutcome;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class UpdateOutcome : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/candidates/{candidateId:long}/outcome",
                async (
                    long vacancyId,
                    long roundId,
                    long candidateId,
                    UpdateOutcomeRequest request,
                    ICommandHandler<UpdateCandidateOutcomeCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpdateCandidateOutcomeCommand(
                            vacancyId,
                            roundId,
                            candidateId,
                            request.Outcome,
                            request.Note),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("UpdateCandidateOutcome");
    }
}