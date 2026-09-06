using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates;

internal static class CandidateWrite
{
    public static async Task<Result<Candidate>> ExecuteAsync(
        long vacancyId,
        long candidateId,
        IApplicationDbContext dbContext,
        Func<Vacancy, Candidate, Result> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(vacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<Candidate>.Failure(CandidateErrors.NotFound(vacancyId));
        }

        var canReviewResult = vacancy.EnsureCanReviewCandidate();
        if (canReviewResult.IsFailure)
        {
            return Result<Candidate>.Failure(canReviewResult.Error);
        }

        var candidate = await dbContext.Candidates
            .Include(item => item.RequirementReviews)
            .SingleOrDefaultAsync(
                item => item.Id == candidateId && item.VacancyId == vacancyId,
                cancellationToken);
        if (candidate is null)
        {
            return Result<Candidate>.Failure(CandidateErrors.NotFound(candidateId));
        }

        var mutationResult = mutation(vacancy, candidate);
        if (mutationResult.IsFailure)
        {
            return Result<Candidate>.Failure(mutationResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return candidate;
    }
}
