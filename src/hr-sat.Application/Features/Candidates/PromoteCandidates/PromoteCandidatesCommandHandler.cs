using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.List;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.PromoteCandidates;

internal sealed class PromoteCandidatesCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<PromoteCandidatesCommand, IReadOnlyList<CandidateSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CandidateSummaryResponse>>> Handle(
        PromoteCandidatesCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.LockVacancyAsync(command.VacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                VacancyErrors.NotFound(command.VacancyId));
        }

        await dbContext.IntakeRounds
            .Where(round => round.VacancyId == command.VacancyId)
            .LoadAsync(cancellationToken);

        var roundIds = vacancy.Rounds.Select(round => round.Id).ToArray();
        await dbContext.Candidates
            .Where(candidate => roundIds.Contains(candidate.IntakeRoundId))
            .LoadAsync(cancellationToken);

        if (vacancy.Status == VacancyStatus.Closed)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                VacancyErrors.Closed(command.VacancyId));
        }

        var targetRound = vacancy.Rounds.SingleOrDefault(round => round.Id == command.RoundId);
        if (targetRound is null)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                IntakeRoundErrors.NotFound(command.RoundId));
        }

        if (!targetRound.IsOpen)
        {
            if (vacancy.ActiveRound is null)
            {
                return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                    IntakeRoundErrors.NoActiveRound(command.VacancyId));
            }

            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                IntakeRoundErrors.Closed(command.RoundId));
        }

        var promotionResult = vacancy.PromoteCandidates(
            command.SourceRoundId,
            command.CandidateIds,
            timeProvider.GetUtcNow());
        if (promotionResult.IsFailure)
        {
            return Result<IReadOnlyList<CandidateSummaryResponse>>.Failure(
                promotionResult.Error);
        }

        var movedCandidateIds = promotionResult.Value
            .Select(candidate => candidate.Id)
            .ToArray();
        await dbContext.SaveChangesAsync(cancellationToken);

        var summaries = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => movedCandidateIds.Contains(candidate.Id))
            .OrderBy(candidate => candidate.ImportedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new CandidateSummaryResponse(
                candidate.Id,
                candidate.FullName,
                candidate.ContactEmail,
                candidate.Notes,
                candidate.ReviewStatus.ToString().ToLowerInvariant(),
                candidate.HireOutcome.ToString().ToLowerInvariant(),
                candidate.SourceSenderName,
                candidate.SourceSenderEmail,
                candidate.SourceSubject,
                candidate.SourceSentAt,
                candidate.CvDocuments.Count))
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return summaries;
    }
}