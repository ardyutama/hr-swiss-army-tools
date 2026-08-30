using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace hr_sat.Server.Features.Vacancies;

internal static class ChangeVacancyStatus
{
    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound, ValidationProblem>> CloseAsync(
        long id,
        AppDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Vacancy? vacancy;
        try
        {
            vacancy = await VacancyWrite.ExecuteAsync(
                id,
                dbContext,
                item => item.Close(timeProvider.GetUtcNow()),
                cancellationToken);
        }
        catch (VacancyValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors);
        }

        return vacancy is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(VacancyDetailsResponse.From(vacancy));
    }

    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound, ValidationProblem>> ReopenAsync(
        long id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Vacancy? vacancy;
        try
        {
            vacancy = await VacancyWrite.ExecuteAsync(
                id,
                dbContext,
                item => item.Reopen(),
                cancellationToken);
        }
        catch (VacancyValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors);
        }

        return vacancy is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(VacancyDetailsResponse.From(vacancy));
    }
}