using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Dispatching;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Dispatches;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Dispatches.RetryFailed;

internal sealed class RetryFailedDispatchesCommandHandler(
    IApplicationDbContext dbContext,
    IEmailSender emailSender,
    IDispatchRunLock dispatchRunLock,
    TimeProvider timeProvider)
    : ICommandHandler<RetryFailedDispatchesCommand, DispatchRunReportResponse>
{
    public async Task<Result<DispatchRunReportResponse>> Handle(
        RetryFailedDispatchesCommand command,
        CancellationToken cancellationToken)
    {
        // Same round-scoped lock as Send To All: a retry never overlaps a run.
        await using var lockHandle = await dispatchRunLock.TryAcquireAsync(
            command.VacancyId,
            command.RoundId,
            cancellationToken);
        if (lockHandle is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                DispatchErrors.RunInProgress(command.VacancyId, command.RoundId));
        }

        if (!emailSender.IsConfigured)
        {
            return Result<DispatchRunReportResponse>.Failure(DispatchErrors.SmtpNotConfigured());
        }

        var vacancyStatus = await dbContext.Vacancies
            .AsNoTracking()
            .Where(item => item.Id == command.VacancyId)
            .Select(item => (VacancyStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (vacancyStatus is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                VacancyErrors.NotFound(command.VacancyId));
        }

        if (vacancyStatus == VacancyStatus.Closed)
        {
            return Result<DispatchRunReportResponse>.Failure(
                DispatchErrors.VacancyClosed(command.VacancyId));
        }

        var roundExists = await dbContext.IntakeRounds
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == command.RoundId && item.VacancyId == command.VacancyId,
                cancellationToken);
        if (!roundExists)
        {
            return Result<DispatchRunReportResponse>.Failure(
                IntakeRoundErrors.NotFound(command.RoundId));
        }

        var run = await dbContext.DispatchRuns
            .SingleOrDefaultAsync(
                item => item.Id == command.RunId &&
                    item.VacancyId == command.VacancyId &&
                    item.IntakeRoundId == command.RoundId,
                cancellationToken);
        if (run is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                DispatchErrors.RunNotFound(command.RunId));
        }

        var outcomes = new List<DispatchOutcomeResponse>();
        var failed = await dbContext.Dispatches
            .Where(dispatch =>
                dispatch.DispatchRunId == run.Id && dispatch.Status == DispatchStatus.Failed)
            .OrderBy(dispatch => dispatch.Id)
            .ToListAsync(cancellationToken);

        if (failed.Count > 0)
        {
            await RetryRowsAsync(failed, outcomes, cancellationToken);
        }

        // Counts are derived, never stored (ADR-0019 amendment): the report totals are
        // the run's rows after the retry.
        var totals = await dbContext.Dispatches
            .AsNoTracking()
            .Where(dispatch => dispatch.DispatchRunId == run.Id)
            .GroupBy(dispatch => dispatch.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // Exclusions belong to the send classification; a retry only ever targets the
        // run's failed rows (ADR-0019, point 3), so the excluded buckets are empty here.
        return new DispatchRunReportResponse(
            run.Id,
            totals.SingleOrDefault(item => item.Status == DispatchStatus.Sent)?.Count ?? 0,
            totals.SingleOrDefault(item => item.Status == DispatchStatus.Failed)?.Count ?? 0,
            0,
            outcomes,
            []);
    }

    private async Task RetryRowsAsync(
        List<Dispatch> failed,
        List<DispatchOutcomeResponse> outcomes,
        CancellationToken cancellationToken)
    {
        var candidateIds = failed.Select(dispatch => dispatch.CandidateId).ToArray();
        var candidates = await dbContext.Candidates
            .Where(candidate => candidateIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);
        var sentElsewhere = (await dbContext.Dispatches
                .AsNoTracking()
                .Where(dispatch =>
                    dispatch.Status == DispatchStatus.Sent &&
                    candidateIds.Contains(dispatch.CandidateId))
                .Select(dispatch => dispatch.CandidateId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var dispatch in failed)
        {
            // A candidate with a successful Dispatch — in any run — is never re-sent
            // (decision 8); the failed row stays as the run's historical record.
            if (sentElsewhere.Contains(dispatch.CandidateId) ||
                !candidates.TryGetValue(dispatch.CandidateId, out var candidate))
            {
                continue;
            }

            var attemptedAt = timeProvider.GetUtcNow();
            if (string.IsNullOrWhiteSpace(candidate.ContactEmail))
            {
                dispatch.MarkFailed("The candidate has no contact email.", attemptedAt);
            }
            else
            {
                try
                {
                    // The rendered snapshot is sent verbatim — later template edits
                    // never rewrite what a run recorded (decision 9).
                    await emailSender.SendAsync(
                        candidate.ContactEmail,
                        dispatch.RenderedSubject,
                        dispatch.RenderedBody,
                        cancellationToken);
                    dispatch.MarkSent(attemptedAt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    dispatch.MarkFailed(exception.Message, attemptedAt);
                }
            }

            var displayName = CandidateDisplayName.Resolve(
                candidate.FullName,
                candidate.SourceSenderName);
            outcomes.Add(new DispatchOutcomeResponse(
                dispatch.CandidateId,
                displayName,
                dispatch.TemplateKind.ToApiValue(),
                dispatch.Status.ToString().ToLowerInvariant(),
                dispatch.ErrorMessage));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (dbContext.IsUniqueViolation(exception))
            {
                // Same backstop as Send To All: the candidate was recorded as sent by a
                // concurrent writer, so this row stays failed and out of the outcomes.
                outcomes.RemoveAll(outcome => outcome.CandidateId == dispatch.CandidateId);
                if (dbContext is DbContext context)
                {
                    context.Entry(dispatch).State = EntityState.Detached;
                }
            }
        }
    }
}
