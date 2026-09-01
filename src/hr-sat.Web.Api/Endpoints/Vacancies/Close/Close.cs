using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Close : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/vacancies/{id:long}/close", async (
            long id,
            ICommandHandler<CloseVacancyCommand, VacancyDetailsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new CloseVacancyCommand(id), cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("CloseVacancy");
    }
}