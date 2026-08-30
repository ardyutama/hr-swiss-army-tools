using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace hr_sat.Server.Features.Vacancies;

internal static class CloseVacancy
{
    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound, ValidationProblem>> HandleAsync(
        long id,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var vacancy = await VacancyWrite.ExecuteAsync(
            id,
            dbContext,
            item => item.Close(timeProvider.GetUtcNow()),
            cancellationToken);

        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await VacancyProgress.GetDetailsAsync(vacancy, dbContext, cancellationToken));
    }
}