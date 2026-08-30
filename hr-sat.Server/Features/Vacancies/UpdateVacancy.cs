using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace hr_sat.Server.Features.Vacancies;

internal static class UpdateVacancy
{
    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound, ValidationProblem>> HandleAsync(
        long id,
        VacancyDefinitionRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var vacancy = await VacancyWrite.ExecuteAsync(
            id,
            dbContext,
            item => item.UpdateDefinition(request.Title, request.OpenedOn, request.Requirements),
            cancellationToken);

        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await VacancyProgress.GetDetailsAsync(vacancy, dbContext, cancellationToken));
    }
}