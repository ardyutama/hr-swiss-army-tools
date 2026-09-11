using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Shared;

internal static class RoundWrite
{
    public static async Task<Result<T>> ExecuteCandidateAsync<T>(
        long vacancyId,
        long roundId,
        long candidateId,
        IApplicationDbContext dbContext,
        Func<Vacancy, long, Result<T>> mutation,
        CancellationToken cancellationToken)
    {
        return await ExecuteLockedAsync(
            vacancyId,
            dbContext,
            async vacancy =>
            {
                await dbContext.IntakeRounds
                    .Where(round => round.VacancyId == vacancyId &&
                        (round.ClosedAt == null || round.Id == roundId))
                    .LoadAsync(cancellationToken);
                await dbContext.Candidates
                    .Where(candidate => candidate.Id == candidateId &&
                        candidate.IntakeRoundId == roundId)
                    .LoadAsync(cancellationToken);

                return mutation(vacancy, candidateId);
            },
            cancellationToken);
    }

    public static async Task<Result<T>> ExecuteAsync<T>(
        long vacancyId,
        long roundId,
        IApplicationDbContext dbContext,
        Func<Vacancy, long, Result<IntakeRound>> guard,
        Func<Vacancy, IntakeRound, Task<Result<T>>> mutation,
        CancellationToken cancellationToken)
    {
        return await ExecuteLockedAsync(
            vacancyId,
            dbContext,
            async vacancy =>
            {
                await dbContext.IntakeRounds
                    .Where(round => round.VacancyId == vacancyId &&
                        (round.ClosedAt == null || round.Id == roundId))
                    .LoadAsync(cancellationToken);

                var roundResult = guard(vacancy, roundId);
                if (roundResult.IsFailure)
                {
                    return Result<T>.Failure(roundResult.Error);
                }

                return await mutation(vacancy, roundResult.Value);
            },
            cancellationToken);
    }

    private static async Task<Result<T>> ExecuteLockedAsync<T>(
        long vacancyId,
        IApplicationDbContext dbContext,
        Func<Vacancy, Task<Result<T>>> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.LockVacancyAsync(vacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<T>.Failure(VacancyErrors.NotFound(vacancyId));
        }

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