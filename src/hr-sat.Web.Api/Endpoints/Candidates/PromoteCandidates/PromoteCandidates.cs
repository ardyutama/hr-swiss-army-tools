using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Features.Candidates.PromoteCandidates;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class PromoteCandidates : IEndpoint
{
    public sealed record Request(long SourceRoundId, IReadOnlyList<long>? CandidateIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/vacancies/{vacancyId:long}/rounds/{roundId:long}/promotions",
                async (
                    long vacancyId,
                    long roundId,
                    Request request,
                    ICommandHandler<
                        PromoteCandidatesCommand,
                        IReadOnlyList<CandidateSummaryResponse>> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new PromoteCandidatesCommand(
                            vacancyId,
                            roundId,
                            request.SourceRoundId,
                            request.CandidateIds),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("PromoteCandidates");
    }
}