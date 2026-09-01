using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.SelectPrimaryCv;
using hr_sat.Application.Features.Candidates.Shared;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class SelectPrimaryCv : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/cv-documents/{documentId:long}/primary",
                async (
                    long vacancyId,
                    long candidateId,
                    long documentId,
                    ICommandHandler<SelectPrimaryCvCommand, CandidateDetailsResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new SelectPrimaryCvCommand(vacancyId, candidateId, documentId),
                        cancellationToken);
                    return result.Match<IResult>(
                        TypedResults.Ok,
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("SelectPrimaryCv");
    }
}
