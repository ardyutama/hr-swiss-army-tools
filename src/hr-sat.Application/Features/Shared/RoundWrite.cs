using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Shared;

internal static class RoundWrite
{
    public static async Task<Result<T>> ExecuteAsync<T>(
        long vacancyId,
        long roundId,
        IApplicationDbContext dbContext,
        Func<Vacancy, long, Result<IntakeRound>> guard,
        Func<Vacancy, IntakeRound, Task<Result<T>>> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.LockVacancyAsync(vacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<T>.Failure(VacancyErrors.NotFound(vacancyId));
        }

        await dbContext.IntakeRounds
            .Where(round => round.VacancyId == vacancyId &&
                (round.ClosedAt == null || round.Id == roundId))
            .LoadAsync(cancellationToken);

        var roundResult = guard(vacancy, roundId);
        if (roundResult.IsFailure)
        {
            return Result<T>.Failure(roundResult.Error);
        }

        var mutationResult = await mutation(vacancy, roundResult.Value);
        if (mutationResult.IsFailure)
        {
            return Result<T>.Failure(mutationResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return mutationResult;
    }
}