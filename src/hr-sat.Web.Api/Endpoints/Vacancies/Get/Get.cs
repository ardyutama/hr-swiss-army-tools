using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vacancies/{id:long}", async (
            long id,
            IQueryHandler<GetVacancyQuery, VacancyDetailsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new GetVacancyQuery(id), cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("GetVacancy");
    }
}