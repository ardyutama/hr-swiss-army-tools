using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class List : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vacancies", async (
            IQueryHandler<ListVacanciesQuery, IReadOnlyList<VacancySummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new ListVacanciesQuery(), cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("ListVacancies");
    }
}