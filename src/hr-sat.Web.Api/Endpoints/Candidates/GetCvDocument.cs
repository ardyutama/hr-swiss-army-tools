using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetCvDocument;

namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed class GetCvDocument : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/cv-documents/{documentId:long}",
                async (
                    long vacancyId,
                    long candidateId,
                    long documentId,
                    IQueryHandler<GetCvDocumentQuery, CvDocumentDownloadResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetCvDocumentQuery(vacancyId, candidateId, documentId),
                        cancellationToken);
                    return result.Match<IResult>(
                        document => TypedResults.File(
                            document.Content,
                            document.ContentType,
                            document.FileName,
                            enableRangeProcessing: true),
                        CustomResults.Problem);
                })
            .WithTags(Tags.Candidates)
            .WithName("GetCvDocument");
    }
}