using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Shared;

internal static class VacancyWrite
{
    public static async Task<Result<Vacancy>> ExecuteAsync(
        long id,
        IApplicationDbContext dbContext,
        Func<Vacancy, Result> mutation,
        CancellationToken cancellationToken)
    {
        return await ExecuteLockedAsync<Vacancy>(
            id,
            dbContext,
            async vacancy =>
            {
                await dbContext.VacancyRequirements
                    .Where(requirement => requirement.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.IntakeRounds
                    .Where(round => round.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.EmailTemplates
                    .Where(template => template.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.FormLayouts
                    .Where(layout => layout.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.ScreeningRuleSets
                    .Where(ruleSet => ruleSet.VacancyId == id)
                    .LoadAsync(cancellationToken);

                var mutationResult = mutation(vacancy);
                if (mutationResult.IsFailure)
                {
                    return Result<Vacancy>.Failure(mutationResult.Error);
                }

                return vacancy;
            },
            cancellationToken);
    }

    public static async Task<Result<Vacancy>> ExecuteAsync(
        long id,
        IApplicationDbContext dbContext,
        Func<Vacancy, Task<Result>> mutation,
        CancellationToken cancellationToken)
    {
        return await ExecuteLockedAsync<Vacancy>(
            id,
            dbContext,
            async vacancy =>
            {
                await dbContext.VacancyRequirements
                    .Where(requirement => requirement.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.IntakeRounds
                    .Where(round => round.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.EmailTemplates
                    .Where(template => template.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.FormLayouts
                    .Where(layout => layout.VacancyId == id)
                    .LoadAsync(cancellationToken);
                await dbContext.ScreeningRuleSets
                    .Where(ruleSet => ruleSet.VacancyId == id)
                    .LoadAsync(cancellationToken);

                var mutationResult = await mutation(vacancy);
                return mutationResult.IsFailure
                    ? Result<Vacancy>.Failure(mutationResult.Error)
                    : vacancy;
            },
            cancellationToken);
    }

    public static async Task<Result<T>> ExecuteLockedAsync<T>(
        long vacancyId,
        IApplicationDbContext dbContext,
        Func<Vacancy, Task<Result<T>>> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(vacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<T>.Failure(VacancyErrors.NotFound(vacancyId));
        }

        await dbContext.FormLayouts
            .Where(layout => layout.VacancyId == vacancyId)
            .LoadAsync(cancellationToken);
        await dbContext.ScreeningRuleSets
            .Where(ruleSet => ruleSet.VacancyId == vacancyId)
            .LoadAsync(cancellationToken);

        var mutationResult = await mutation(vacancy);
        if (mutationResult.IsFailure)
        {
            return Result<T>.Failure(mutationResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return mutationResult;
    }
}