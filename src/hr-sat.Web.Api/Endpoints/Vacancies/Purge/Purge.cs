using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Purge : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/vacancies/{id:long}", async (
            long id,
            ICommandHandler<PurgeVacancyCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new PurgeVacancyCommand(id), cancellationToken);
            return result.Match<IResult>(
                TypedResults.NoContent,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("PurgeVacancy");
    }
}