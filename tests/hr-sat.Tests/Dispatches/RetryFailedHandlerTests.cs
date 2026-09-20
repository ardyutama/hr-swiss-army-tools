using hr_sat.Application.Abstractions.Dispatching;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Features.Dispatches.RetryFailed;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Dispatches;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Dispatches;

public sealed class RetryFailedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_Should_ResendTheStoredSnapshot_WhenRetrying() // domain: Dispatch — the snapshot is sent verbatim, never re-rendered
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var candidate = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "alice@example.com");
        // No template row on purpose: the retry resends the recorded snapshot even
        // when the template has changed or is gone (decision 9).
        var run = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        var failed = AddFailedRow(dbContext, run.Id, candidate.Id);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, run.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.RunId.ShouldBe(run.Id);
        report.SentCount.ShouldBe(1);
        report.FailedCount.ShouldBe(0);
        report.ExcludedCount.ShouldBe(0);
        var outcome = report.Outcomes.ShouldHaveSingleItem();
        outcome.CandidateId.ShouldBe(candidate.Id);
        outcome.CandidateName.ShouldBe("Candidate 1");
        outcome.TemplateKind.ShouldBe("rejected");
        outcome.Status.ShouldBe("sent");
        outcome.Error.ShouldBeNull();

        await emailSender.Received(1).SendAsync(
            "alice@example.com",
            "Original subject",
            "Original body",
            Arg.Any<CancellationToken>());

        var persisted = await dbContext.Dispatches.SingleAsync();
        persisted.Status.ShouldBe(DispatchStatus.Sent);
        persisted.ErrorMessage.ShouldBeNull();
        persisted.AttemptedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Handle_Should_RecordFailureWithoutSending_WhenCandidateHasNoContactEmail() // domain: Dispatch
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var candidate = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, null);
        var run = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        AddFailedRow(dbContext, run.Id, candidate.Id);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, run.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(0);
        report.FailedCount.ShouldBe(1);
        var outcome = report.Outcomes.ShouldHaveSingleItem();
        outcome.Status.ShouldBe("failed");
        outcome.Error.ShouldBe("The candidate has no contact email.");
        await emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_SkipCandidate_WhenASuccessfulDispatchExistsInAnyRun() // domain: Dispatch — sent once, ever
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var candidate = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "done@example.com");
        var run = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        AddFailedRow(dbContext, run.Id, candidate.Id);
        var laterRun = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        dbContext.Dispatches.Add(Dispatch.RecordSent(
            laterRun.Id,
            candidate.Id,
            EmailTemplateKind.Rejected,
            "Later subject",
            "Later body",
            Now.AddMinutes(-30)));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, run.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(0);
        report.FailedCount.ShouldBe(1);
        report.Outcomes.ShouldBeEmpty();
        await emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        var persisted = await dbContext.Dispatches
            .SingleAsync(item => item.DispatchRunId == run.Id);
        persisted.Status.ShouldBe(DispatchStatus.Failed);
    }

    [Fact]
    public async Task Handle_Should_KeepRowFailedAndOutOfOutcomes_WhenTheUpdateRaces() // domain: Dispatch — 23505 leaves the row as the run's historical record
    {
        await using var dbContext = new UniqueViolationOnceDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var candidate = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "raced@example.com");
        var run = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        AddFailedRow(dbContext, run.Id, candidate.Id);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        // The retry's first save is the per-row commit; the violation hits it.
        dbContext.ThrowUniqueViolationAfterSaves(0);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, run.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(0);
        report.FailedCount.ShouldBe(1);
        report.Outcomes.ShouldBeEmpty();
        var persisted = await dbContext.Dispatches
            .AsNoTracking()
            .SingleAsync(item => item.DispatchRunId == run.Id);
        persisted.Status.ShouldBe(DispatchStatus.Failed);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRunDoesNotExist() // domain: Dispatch Run
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.RunNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRunBelongsToAnotherRound() // domain: Dispatch Run — retries are run-scoped
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        vacancy.CloseRound(round.Id, Now).IsSuccess.ShouldBeTrue();
        var otherRoundResult = vacancy.CreateRound("Second wave");
        otherRoundResult.IsSuccess.ShouldBeTrue();
        var run = await SeedRunAsync(dbContext, vacancy.Id, round.Id);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, otherRoundResult.Value.Id, run.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.RunNotFound");
    }

    [Fact]
    public async Task Handle_Should_FailFast_WhenAnotherRunHoldsTheLock() // domain: Dispatch Run
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, CreateEmailSender(), CreateHeldLock());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.RunInProgress");
    }

    [Fact]
    public async Task Handle_Should_Refuse_WhenSmtpIsNotConfigured() // domain: Dispatch
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, CreateEmailSender(configured: false));

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.SmtpNotConfigured");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenVacancyIsClosed() // domain: Closed Vacancy — a retry never revives a closed vacancy
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext, closed: true);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, round.Id, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.VacancyClosed");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // domain: Vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(999, 1, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Vacancies.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRoundDoesNotExist() // domain: Intake Round
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, _) = await SeedVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new RetryFailedDispatchesCommand(vacancy.Id, 999, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("IntakeRounds.NotFound");
    }

    private static async Task<(Vacancy Vacancy, IntakeRound Round)> SeedVacancyAsync(
        TestDbContext dbContext,
        bool closed = false)
    {
        var vacancy = CandidateTestData.CreateVacancy(closed);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return (vacancy, vacancy.Rounds.Single());
    }

    private static async Task<Candidate> AddCandidateAsync(
        TestDbContext dbContext,
        long roundId,
        int sourceNumber,
        CandidateReviewStatus reviewStatus,
        string? contactEmail)
    {
        var candidate = CandidateTestData.CreateCandidate(roundId, sourceNumber);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        candidate.ApplyReview(reviewStatus, null).IsSuccess.ShouldBeTrue();
        if (contactEmail is not null)
        {
            candidate.UpdateDetails($"Candidate {sourceNumber}", contactEmail)
                .IsSuccess.ShouldBeTrue();
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
        return candidate;
    }

    private static async Task<DispatchRun> SeedRunAsync(
        TestDbContext dbContext,
        long vacancyId,
        long roundId)
    {
        var run = DispatchRun.Start(vacancyId, roundId, Now.AddHours(-1));
        dbContext.DispatchRuns.Add(run);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return run;
    }

    private static Dispatch AddFailedRow(
        TestDbContext dbContext,
        long runId,
        long candidateId)
    {
        var dispatch = Dispatch.RecordFailed(
            runId,
            candidateId,
            EmailTemplateKind.Rejected,
            "Original subject",
            "Original body",
            "SMTP offline.",
            Now.AddHours(-1));
        dbContext.Dispatches.Add(dispatch);
        return dispatch;
    }

    private static RetryFailedDispatchesCommandHandler CreateHandler(
        TestDbContext dbContext,
        IEmailSender emailSender,
        IDispatchRunLock? dispatchRunLock = null) =>
        new(
            dbContext,
            emailSender,
            dispatchRunLock ?? CreateAcquiredLock(),
            new FixedTimeProvider(Now));

    private static IEmailSender CreateEmailSender(bool configured = true)
    {
        var sender = Substitute.For<IEmailSender>();
        sender.IsConfigured.Returns(configured);
        sender.SendAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        return sender;
    }

    private static IDispatchRunLock CreateAcquiredLock()
    {
        var runLock = Substitute.For<IDispatchRunLock>();
        runLock.TryAcquireAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IAsyncDisposable?>(Substitute.For<IAsyncDisposable>()));
        return runLock;
    }

    private static IDispatchRunLock CreateHeldLock()
    {
        var runLock = Substitute.For<IDispatchRunLock>();
        runLock.TryAcquireAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IAsyncDisposable?>(null));
        return runLock;
    }
}
