using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Vacancies;

namespace hr_sat.Web.Api.Endpoints.Vacancies;

internal sealed class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/vacancies/{id:long}", async (
            long id,
            Request request,
            ICommandHandler<UpdateVacancyCommand, VacancyDetailsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateVacancyCommand(
                id,
                request.Title,
                request.OpenedOn,
                request.Requirements);
            var result = await handler.Handle(command, cancellationToken);
            return result.Match<IResult>(
                TypedResults.Ok,
                CustomResults.Problem);
        })
        .WithTags(Tags.Vacancies)
        .WithName("UpdateVacancy");
    }

    public sealed record Request(
        string? Title,
        DateOnly OpenedOn,
        IReadOnlyList<string?>? Requirements);
}