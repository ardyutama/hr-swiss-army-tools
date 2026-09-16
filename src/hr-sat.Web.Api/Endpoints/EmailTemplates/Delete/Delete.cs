using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.EmailTemplates.Delete;

namespace hr_sat.Web.Api.Endpoints.EmailTemplates;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/api/vacancies/{vacancyId:long}/email-templates/{kind}",
                async (
                    long vacancyId,
                    string kind,
                    ICommandHandler<DeleteEmailTemplateCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new DeleteEmailTemplateCommand(vacancyId, kind),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.NoContent, CustomResults.Problem);
                })
            .WithTags(Tags.EmailTemplates)
            .WithName("DeleteEmailTemplate");
    }
}