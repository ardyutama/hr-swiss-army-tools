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
        return await VacancyWrite.ExecuteLockedAsync(
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
                await dbContext.CandidateFormResponses
                    .Where(response => response.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);
                await dbContext.CvDocuments
                    .Where(document => document.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);
                await dbContext.CandidateRequirementReviews
                    .Where(review => review.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);

                return mutation(vacancy, candidateId);
            },
            cancellationToken);
    }

    public static async Task<Result<T>> ExecuteCandidateAsync<T>(
        long vacancyId,
        long roundId,
        long candidateId,
        IApplicationDbContext dbContext,
        Func<Vacancy, long, Task<Result<T>>> mutation,
        CancellationToken cancellationToken)
    {
        return await VacancyWrite.ExecuteLockedAsync(
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
                await dbContext.CandidateFormResponses
                    .Where(response => response.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);
                await dbContext.CvDocuments
                    .Where(document => document.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);
                await dbContext.CandidateRequirementReviews
                    .Where(review => review.CandidateId == candidateId)
                    .LoadAsync(cancellationToken);

                return await mutation(vacancy, candidateId);
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
        return await VacancyWrite.ExecuteLockedAsync(
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

}