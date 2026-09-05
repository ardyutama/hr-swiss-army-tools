using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.UpdateDetails;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class UpdateDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/details",
                async (
                    long vacancyId,
                    long candidateId,
                    UpdateDetailsRequest request,
                    ICommandHandler<UpdateCandidateDetailsCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpdateCandidateDetailsCommand(
                            vacancyId,
                            candidateId,
                            request.FullName,
                            request.ContactEmail),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("UpdateCandidateDetails");
    }
}
