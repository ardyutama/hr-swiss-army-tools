using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace hr_sat.Server.Features.Vacancies;

internal static class UpdateVacancy
{
    internal sealed record Request(
        string? Title,
        DateOnly OpenedOn,
        IReadOnlyList<string?>? Requirements);

    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound, ValidationProblem>> HandleAsync(
        long id,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Vacancy? vacancy;
        try
        {
            vacancy = await VacancyWrite.ExecuteAsync(
                id,
                dbContext,
                item => item.UpdateDefinition(request.Title, request.OpenedOn, request.Requirements),
                cancellationToken);
        }
        catch (VacancyValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors);
        }

        if (vacancy is null)
        {
            return TypedResults.NotFound();
        }

        var progress = await VacancyProgress.GetAsync(id, dbContext, cancellationToken);
        return TypedResults.Ok(VacancyDetailsResponse.From(vacancy, progress));
    }
}