using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Reopen : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/vacancies/{id:long}/reopen", async (
            long id,
            ICommandHandler<ReopenVacancyCommand, VacancyDetailsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new ReopenVacancyCommand(id), cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("ReopenVacancy");
    }
}