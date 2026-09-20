using hr_sat.Application.Abstractions.Dispatching;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Features.Dispatches;
using hr_sat.Application.Features.Dispatches.SendToAll;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.Dispatches;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Dispatches;

public sealed class SendToAllHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_Should_SendToEveryContactableCandidate_WhenRunSucceeds() // US-19
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Shortlisted, "alice@example.com");
        await AddCandidateAsync(
            dbContext, round.Id, 2, CandidateReviewStatus.Rejected, "bob@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Shortlisted);
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.RunId.ShouldNotBeNull();
        report.SentCount.ShouldBe(2);
        report.FailedCount.ShouldBe(0);
        report.ExcludedCount.ShouldBe(0);
        report.Outcomes.Select(outcome => outcome.Status).ShouldBe(["sent", "sent"]);
        report.Outcomes.Select(outcome => outcome.TemplateKind)
            .ShouldBe(["shortlisted", "rejected"]);

        await emailSender.Received(1).SendAsync(
            "alice@example.com",
            "Hello Candidate 1",
            "About Data Analyst.",
            Arg.Any<CancellationToken>());
        await emailSender.Received(1).SendAsync(
            "bob@example.com",
            "Hello Candidate 2",
            "About Data Analyst.",
            Arg.Any<CancellationToken>());

        var run = await dbContext.DispatchRuns.SingleAsync();
        run.CompletedAt.ShouldBe(Now);
        (await dbContext.Dispatches.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_FailFast_WhenAnotherRunHoldsTheLock() // domain: Dispatch Run
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var handler = CreateHandler(
            dbContext,
            CreateEmailSender(),
            CreateHeldLock());

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.RunInProgress");
        (await dbContext.DispatchRuns.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RefuseWithoutWritingRows_WhenSmtpIsNotConfigured() // domain: Dispatch
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "bob@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender(configured: false);
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.SmtpNotConfigured");
        (await dbContext.DispatchRuns.CountAsync()).ShouldBe(0);
        (await dbContext.Dispatches.CountAsync()).ShouldBe(0);
        await emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // domain: Vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new SendToAllCommand(999, 1),
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
            new SendToAllCommand(vacancy.Id, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("IntakeRounds.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenVacancyIsClosed() // domain: Closed Vacancy
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext, closed: true);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.VacancyClosed");
        (await dbContext.DispatchRuns.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_Send_WhenRoundIsClosedButVacancyIsOpen() // domain: Dispatch — a closed round is a legal audience while the vacancy is open
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "bob@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        vacancy.CloseRound(round.Id, Now.AddDays(-1)).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SentCount.ShouldBe(1);
        await emailSender.Received(1).SendAsync(
            "bob@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_RefuseBeforeWritingRows_WhenANeededTemplateIsMissing() // US-19 — no partial silent sends
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "bob@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Shortlisted);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Dispatch.MissingTemplate");
        result.Error.Extensions.ShouldNotBeNull()["kind"].ShouldBe("rejected");
        (await dbContext.DispatchRuns.CountAsync()).ShouldBe(0);
        (await dbContext.Dispatches.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_Send_WhenOnlyAnUnusedBucketTemplateIsMissing() // US-19 — only buckets present in the audience need a template
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Shortlisted, "alice@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Shortlisted);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SentCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_NameExclusionBucketsWithReasons_WhenClassifying() // domain: Contactable Candidate
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var undecided = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Flagged, "new@example.com");
        var outcomeRecorded = await AddCandidateAsync(
            dbContext, round.Id, 2, CandidateReviewStatus.Shortlisted, "hired@example.com");
        outcomeRecorded.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();
        var missingEmail = await AddCandidateAsync(
            dbContext, round.Id, 3, CandidateReviewStatus.Rejected, null);
        var contactable = await AddCandidateAsync(
            dbContext, round.Id, 4, CandidateReviewStatus.Rejected, "rejected@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(1);
        report.ExcludedCount.ShouldBe(3);
        report.Excluded.ShouldSatisfyAllConditions(
            () => report.Excluded.Single(item => item.CandidateId == undecided.Id)
                .Reason.ShouldBe("undecided"),
            () => report.Excluded.Single(item => item.CandidateId == outcomeRecorded.Id)
                .Reason.ShouldBe("outcome-recorded"),
            () => report.Excluded.Single(item => item.CandidateId == missingEmail.Id)
                .Reason.ShouldBe("missing-email"));
        report.Excluded.ShouldNotContain(item => item.CandidateId == contactable.Id);
    }

    [Fact]
    public async Task Handle_Should_KeepScreenedOutCandidatesOutOfTheReport_WhenSending() // US-19 — Screened Out candidates are filtered before classification
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        AddScreening(dbContext, vacancy.Id);
        var screenedOut = AddFormCandidate(dbContext, round.Id, "No", "screened@example.com");
        var passes = AddFormCandidate(dbContext, round.Id, "Yes", "passes@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(1);
        report.Outcomes.ShouldNotContain(item => item.CandidateId == screenedOut.Id);
        report.Excluded.ShouldNotContain(item => item.CandidateId == screenedOut.Id);
        report.Outcomes.ShouldContain(item => item.CandidateId == passes.Id);
        await emailSender.DidNotReceive().SendAsync(
            "screened@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_SkipCandidate_WhenASuccessfulDispatchAlreadyExists() // domain: Dispatch — a successful Dispatch per Candidate is recorded once, ever
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        var alreadySent = await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "done@example.com");
        var fresh = await AddCandidateAsync(
            dbContext, round.Id, 2, CandidateReviewStatus.Rejected, "fresh@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        var earlierRun = DispatchRun.Start(vacancy.Id, round.Id, Now.AddDays(-1));
        dbContext.DispatchRuns.Add(earlierRun);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.Dispatches.Add(Dispatch.RecordSent(
            earlierRun.Id,
            alreadySent.Id,
            EmailTemplateKind.Rejected,
            "Earlier subject",
            "Earlier body",
            Now.AddDays(-1)));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(1);
        report.Outcomes.ShouldNotContain(item => item.CandidateId == alreadySent.Id);
        report.Outcomes.ShouldContain(item => item.CandidateId == fresh.Id);
        await emailSender.DidNotReceive().SendAsync(
            "done@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_CountUniqueViolationAsAlreadySent_WhenTheInsertRaces() // domain: Dispatch — 23505 is already-sent, never a failure
    {
        await using var dbContext = new UniqueViolationOnceDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "raced@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        // The run-row insert is the first save inside the handler; the violation hits
        // the Dispatch row commit right after it.
        dbContext.ThrowUniqueViolationAfterSaves(1);
        var emailSender = CreateEmailSender();
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.RunId.ShouldNotBeNull();
        report.SentCount.ShouldBe(0);
        report.FailedCount.ShouldBe(0);
        report.Outcomes.ShouldBeEmpty();
        var run = await dbContext.DispatchRuns.SingleAsync();
        run.CompletedAt.ShouldNotBeNull();
        (await dbContext.Dispatches.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ContinueAndKeepTheSnapshot_WhenOneSendFails() // US-19 — per-candidate failure never aborts the run
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Rejected, "fine@example.com");
        var failing = await AddCandidateAsync(
            dbContext, round.Id, 2, CandidateReviewStatus.Rejected, "down@example.com");
        AddTemplate(dbContext, vacancy.Id, EmailTemplateKind.Rejected);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var emailSender = CreateEmailSender();
        emailSender
            .SendAsync(
                "down@example.com",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP offline.")));
        var handler = CreateHandler(dbContext, emailSender);

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.SentCount.ShouldBe(1);
        report.FailedCount.ShouldBe(1);
        var failure = report.Outcomes.Single(item => item.CandidateId == failing.Id);
        failure.Status.ShouldBe("failed");
        failure.Error.ShouldBe("SMTP offline.");

        var failedRow = await dbContext.Dispatches
            .SingleAsync(item => item.CandidateId == failing.Id);
        failedRow.Status.ShouldBe(DispatchStatus.Failed);
        failedRow.ErrorMessage.ShouldBe("SMTP offline.");
        failedRow.RenderedSubject.ShouldBe("Hello Candidate 2");
        failedRow.RenderedBody.ShouldBe("About Data Analyst.");
        failedRow.AttemptedAt.ShouldBe(Now);
        var run = await dbContext.DispatchRuns.SingleAsync();
        run.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNullRunId_WhenNobodyCanBeContacted() // domain: Dispatch — no run row without Dispatches
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, round) = await SeedVacancyAsync(dbContext);
        await AddCandidateAsync(
            dbContext, round.Id, 1, CandidateReviewStatus.Flagged, "pending@example.com");
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(dbContext, CreateEmailSender());

        var result = await handler.Handle(
            new SendToAllCommand(vacancy.Id, round.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;
        report.RunId.ShouldBeNull();
        report.SentCount.ShouldBe(0);
        report.ExcludedCount.ShouldBe(1);
        (await dbContext.DispatchRuns.CountAsync()).ShouldBe(0);
        (await dbContext.Dispatches.CountAsync()).ShouldBe(0);
    }

    private static async Task<(Vacancy Vacancy, hr_sat.Domain.IntakeRounds.IntakeRound Round)> SeedVacancyAsync(
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

    private static Candidate AddFormCandidate(
        TestDbContext dbContext,
        long roundId,
        string availability,
        string contactEmail)
    {
        var importedAt = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
        var candidateResult = Candidate.ImportForm(new CandidateFormImportData(roundId, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var candidate = candidateResult.Value;
        candidate.AddFormResponse(
            new CandidateFormResponseData(
                ["2026-09-19T10:00:00Z", "Form Person", contactEmail, availability],
                "2026-09-19T10:00:00Z",
                importedAt,
                contactEmail,
                importedAt),
            currentResponse: null,
            isResubmitted: false).IsSuccess.ShouldBeTrue();
        candidate.ApplyReview(CandidateReviewStatus.Rejected, null).IsSuccess.ShouldBeTrue();
        candidate.UpdateDetails("Form Person", contactEmail).IsSuccess.ShouldBeTrue();
        dbContext.Candidates.Add(candidate);
        return candidate;
    }

    private static void AddScreening(TestDbContext dbContext, long vacancyId)
    {
        var layoutResult = FormLayout.Create(
            vacancyId,
            ["Timestamp", "Name", "Email", "Availability"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, null, "Availability")
            ]));
        layoutResult.IsSuccess.ShouldBeTrue();
        var ruleSetResult = ScreeningRuleSet.Create(
            vacancyId,
            layoutResult.Value,
            new ScreeningRuleDefinition([
                new ScreeningRule(3, ScreeningOperator.Equals, "No")
            ]));
        ruleSetResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(layoutResult.Value);
        dbContext.ScreeningRuleSets.Add(ruleSetResult.Value);
    }

    private static void AddTemplate(
        TestDbContext dbContext,
        long vacancyId,
        EmailTemplateKind kind)
    {
        var template = EmailTemplate.Create(
            vacancyId,
            kind,
            "Hello {{candidate_name}}",
            "About {{vacancy_title}}.");
        template.IsSuccess.ShouldBeTrue();
        dbContext.EmailTemplates.Add(template.Value);
    }

    private static SendToAllCommandHandler CreateHandler(
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
