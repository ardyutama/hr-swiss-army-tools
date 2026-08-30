using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace hr_sat.Server.Features.Vacancies;

internal static class CreateVacancy
{
    internal sealed record Request(
        string? Title,
        DateOnly OpenedOn,
        IReadOnlyList<string?>? Requirements);

    public static async Task<Results<Created<VacancyDetailsResponse>, ValidationProblem>> HandleAsync(
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var vacancy = Vacancy.Create(request.Title, request.OpenedOn, request.Requirements);

        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/vacancies/{vacancy.Id}",
            VacancyDetailsResponse.From(vacancy));
    }
}