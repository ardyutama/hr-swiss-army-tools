using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Dispatching;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Dispatches;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Dispatches.SendToAll;

internal sealed class SendToAllCommandHandler(
    IApplicationDbContext dbContext,
    IEmailSender emailSender,
    IDispatchRunLock dispatchRunLock,
    ISmtpSettingsStore smtpSettingsStore,
    TimeProvider timeProvider)
    : ICommandHandler<SendToAllCommand, DispatchRunReportResponse>
{
    public async Task<Result<DispatchRunReportResponse>> Handle(
        SendToAllCommand command,
        CancellationToken cancellationToken)
    {
        // The lock outlives the per-candidate commits (append-as-you-go), so it is held
        // on its own connection for the whole request; a concurrent run fails fast.
        await using var lockHandle = await dispatchRunLock.TryAcquireAsync(
            command.VacancyId,
            command.RoundId,
            cancellationToken);
        if (lockHandle is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                DispatchErrors.RunInProgress(command.VacancyId, command.RoundId));
        }

        // The dispatch pre-flight (issue 03, decisions 7, 20): a Microsoft Account
        // grant silently refreshes once here; a dead grant refuses the whole run
        // before any dispatch row is written.
        var readiness = await emailSender.CheckReadinessAsync(cancellationToken);
        if (readiness == SmtpReadiness.NotConfigured)
        {
            return Result<DispatchRunReportResponse>.Failure(DispatchErrors.SmtpNotConfigured());
        }

        if (readiness == SmtpReadiness.SignInExpired)
        {
            return Result<DispatchRunReportResponse>.Failure(DispatchErrors.SmtpSignInExpired());
        }

        // The account resolves once per run (issue 02, decision 13): every Dispatch row
        // of the run records the same sender, like the templates it rendered.
        // Readiness passed, so the effective settings are complete — FromAddress is
        // non-null.
        var fromAddress = (await smtpSettingsStore.GetSnapshotAsync(cancellationToken)).FromAddress!;

        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Where(item => item.Id == command.VacancyId)
            .Select(item => new { item.Title, item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (vacancy is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                VacancyErrors.NotFound(command.VacancyId));
        }

        if (vacancy.Status == VacancyStatus.Closed)
        {
            return Result<DispatchRunReportResponse>.Failure(
                DispatchErrors.VacancyClosed(command.VacancyId));
        }

        // The viewed round, open or closed — dispatching into a closed round is legal
        // while the vacancy is open (ADR-0019, point 4).
        var round = await dbContext.IntakeRounds
            .AsNoTracking()
            .Where(item => item.Id == command.RoundId && item.VacancyId == command.VacancyId)
            .Select(item => new { item.ClosedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return Result<DispatchRunReportResponse>.Failure(
                IntakeRoundErrors.NotFound(command.RoundId));
        }

        var screeningContext = await ScreeningContextLoader.LoadAsync(
            command.VacancyId,
            round.ClosedAt is not null,
            dbContext,
            cancellationToken);
        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.IntakeRoundId == command.RoundId)
            // EvaluateScreening reads the current form response; without the include
            // no rule ever fires and screened-out candidates would be sent to.
            .Include(candidate => candidate.FormResponses)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        var audience = new List<AudienceMember>();
        var excluded = new List<DispatchExclusionResponse>();
        foreach (var candidate in candidates)
        {
            // Screened Out candidates are filtered before classification and never
            // appear in the report (decision 14).
            if (candidate.EvaluateScreening(screeningContext).Count > 0)
            {
                continue;
            }

            var evaluation = Contactability.Evaluate(
                candidate.ReviewStatus,
                candidate.HireOutcome,
                candidate.ContactEmail);
            var displayName = CandidateDisplayName.Resolve(
                candidate.FullName,
                candidate.SourceSenderName);
            if (evaluation.Kind == ContactabilityKind.Contactable)
            {
                audience.Add(new AudienceMember(
                    candidate.Id,
                    candidate.ContactEmail!,
                    displayName,
                    evaluation.TemplateKind!.Value));
            }
            else
            {
                excluded.Add(new DispatchExclusionResponse(
                    candidate.Id,
                    displayName,
                    evaluation.Kind.ToApiValue()));
            }
        }

        var templates = await dbContext.EmailTemplates
            .AsNoTracking()
            .Where(template => template.VacancyId == command.VacancyId)
            .ToListAsync(cancellationToken);

        // A missing template kind blocks the whole send before any row is written —
        // no partial silent sends (decision 4).
        foreach (var kind in audience.Select(member => member.TemplateKind).Distinct())
        {
            if (templates.All(template => template.Kind != kind))
            {
                return Result<DispatchRunReportResponse>.Failure(
                    DispatchErrors.MissingTemplate(kind));
            }
        }

        var audienceIds = audience.Select(member => member.CandidateId).ToArray();
        var alreadySent = (await dbContext.Dispatches
                .AsNoTracking()
                .Where(dispatch =>
                    dispatch.Status == DispatchStatus.Sent &&
                    audienceIds.Contains(dispatch.CandidateId))
                .Select(dispatch => dispatch.CandidateId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var toSend = audience
            .Where(member => !alreadySent.Contains(member.CandidateId))
            .ToArray();
        if (toSend.Length == 0)
        {
            // Nothing to send: no run row without Dispatches (decision 20). The report
            // still carries the exclusions so the console can explain the outcome.
            return new DispatchRunReportResponse(null, 0, 0, excluded.Count, [], excluded);
        }

        var run = DispatchRun.Start(
            command.VacancyId,
            command.RoundId,
            timeProvider.GetUtcNow());
        dbContext.DispatchRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        var attempted = new List<(Dispatch Dispatch, string CandidateName)>();
        foreach (var member in toSend)
        {
            var template = templates.Single(item => item.Kind == member.TemplateKind);
            var rendered = EmailTemplateRenderer.Render(
                template.Subject,
                template.Body,
                member.CandidateName,
                vacancy.Title);
            var attemptedAt = timeProvider.GetUtcNow();

            Dispatch dispatch;
            try
            {
                await emailSender.SendAsync(
                    member.ContactEmail,
                    rendered.Subject,
                    rendered.Body,
                    cancellationToken);
                dispatch = Dispatch.RecordSent(
                    run.Id,
                    member.CandidateId,
                    member.TemplateKind,
                    rendered.Subject,
                    rendered.Body,
                    fromAddress,
                    attemptedAt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                dispatch = Dispatch.RecordFailed(
                    run.Id,
                    member.CandidateId,
                    member.TemplateKind,
                    rendered.Subject,
                    rendered.Body,
                    fromAddress,
                    exception.Message,
                    attemptedAt);
            }

            // Append-as-you-go: each Dispatch row commits right after its attempt, so a
            // crash leaves an interrupted run (completed_at NULL) instead of a rollback
            // that would lose the idempotency record (decision 20).
            dbContext.Dispatches.Add(dispatch);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                attempted.Add((dispatch, member.CandidateName));
            }
            catch (DbUpdateException exception) when (dbContext.IsUniqueViolation(exception))
            {
                // The filtered unique index is the database backstop of the idempotency
                // rule: a successful Dispatch for this candidate already exists, so the
                // attempt counts as already-sent and is left out of the outcomes
                // (decision 19) — never as a failure.
                if (dbContext is DbContext context)
                {
                    context.Entry(dispatch).State = EntityState.Detached;
                }
            }
        }

        run.Complete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        // Counts are derived from the run's persisted rows, never stored and never
        // counted in memory — a stored count is a cache a mid-run crash corrupts
        // (ADR-0019 amendment; same derivation as the retry handler).
        var totals = await dbContext.Dispatches
            .AsNoTracking()
            .Where(dispatch => dispatch.DispatchRunId == run.Id)
            .GroupBy(dispatch => dispatch.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var outcomes = attempted
            .Select(item => new DispatchOutcomeResponse(
                item.Dispatch.CandidateId,
                item.CandidateName,
                item.Dispatch.TemplateKind.ToApiValue(),
                item.Dispatch.Status.ToString().ToLowerInvariant(),
                item.Dispatch.ErrorMessage))
            .ToArray();

        return new DispatchRunReportResponse(
            run.Id,
            totals.SingleOrDefault(item => item.Status == DispatchStatus.Sent)?.Count ?? 0,
            totals.SingleOrDefault(item => item.Status == DispatchStatus.Failed)?.Count ?? 0,
            excluded.Count,
            outcomes,
            excluded);
    }

    private sealed record AudienceMember(
        long CandidateId,
        string ContactEmail,
        string CandidateName,
        EmailTemplateKind TemplateKind);
}
