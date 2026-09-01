using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/vacancies", async (
            CreateVacancyCommand command,
            ICommandHandler<CreateVacancyCommand, VacancyDetailsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(command, cancellationToken);
            return result.Match<IResult>(
                vacancy => TypedResults.Created($"/api/vacancies/{vacancy.Id}", vacancy),
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("CreateVacancy");
    }
}