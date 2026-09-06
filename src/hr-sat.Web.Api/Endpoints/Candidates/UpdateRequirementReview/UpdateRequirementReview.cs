using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.UpdateRequirementReview;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class UpdateRequirementReview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/requirement-reviews/{requirementId:long}",
                async (
                    long vacancyId,
                    long candidateId,
                    long requirementId,
                    UpdateRequirementReviewRequest request,
                    ICommandHandler<UpdateCandidateRequirementReviewCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpdateCandidateRequirementReviewCommand(
                            vacancyId,
                            candidateId,
                            requirementId,
                            request.Confirmed),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("UpdateCandidateRequirementReview");
    }
}
