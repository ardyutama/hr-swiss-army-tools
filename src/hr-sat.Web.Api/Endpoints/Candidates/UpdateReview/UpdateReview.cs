using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.UpdateReview;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class UpdateReview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/review",
                async (
                    long vacancyId,
                    long candidateId,
                    UpdateReviewRequest request,
                    ICommandHandler<UpdateCandidateReviewCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpdateCandidateReviewCommand(
                            vacancyId,
                            candidateId,
                            request.ReviewStatus,
                            request.Notes),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("UpdateCandidateReview");
    }
}
