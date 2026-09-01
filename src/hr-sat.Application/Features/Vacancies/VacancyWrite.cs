using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Vacancies;

internal static class VacancyWrite
{
    public static async Task<Result<Vacancy>> ExecuteAsync(
        long id,
        IApplicationDbContext dbContext,
        Func<Vacancy, Result> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(id, cancellationToken);
        if (vacancy is null)
        {
            return Result<Vacancy>.Failure(VacancyErrors.NotFound(id));
        }

        var mutationResult = mutation(vacancy);
        if (mutationResult.IsFailure)
        {
            return Result<Vacancy>.Failure(mutationResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return vacancy;
    }
}