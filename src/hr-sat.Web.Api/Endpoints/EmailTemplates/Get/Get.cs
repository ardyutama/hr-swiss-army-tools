using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailTemplates;
using hr_sat.Application.Features.EmailTemplates.Get;

namespace hr_sat.Web.Api.Endpoints.EmailTemplates;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vacancies/{vacancyId:long}/email-templates",
                async (
                    long vacancyId,
                    IQueryHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateResponse>> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new GetEmailTemplatesQuery(vacancyId),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.EmailTemplates)
            .WithName("GetEmailTemplates");
    }
}