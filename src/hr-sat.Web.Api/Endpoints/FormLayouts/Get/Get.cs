using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.FormLayouts;
using hr_sat.Application.Features.FormLayouts.Get;

namespace hr_sat.Web.Api.Endpoints.FormLayouts;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/form-layout",
                async (
                    long vacancyId,
                    IQueryHandler<GetFormLayoutQuery, FormLayoutResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetFormLayoutQuery(vacancyId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.FormLayouts)
            .WithName("GetFormLayout");
    }
}