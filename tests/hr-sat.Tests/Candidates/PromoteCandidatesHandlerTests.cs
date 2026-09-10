using hr_sat.Application.Features.Candidates.PromoteCandidates;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class PromoteCandidatesHandlerTests
{
    private static readonly DateTimeOffset FirstPromotionAt =
        new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_Should_MoveCandidateAndPreserveReviewData_WhenCandidateIsPromotable() // domain: Promote preserves candidate review data
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, sourceRound, activeRound) = await CreateRoundsAsync(dbContext);
        var candidate = CandidateTestData.CreateCandidate(sourceRound.Id);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        candidate.ApplyReview(CandidateReviewStatus.Flagged, "Keep this note.").IsSuccess.ShouldBeTrue();
        candidate.SetRequirementReview(vacancy.Requirements.Single().Id, true).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(
                vacancy.Id,
                activeRound.Id,
                sourceRound.Id,
                [candidate.Id]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value.ShouldHaveSingleItem();
        summary.Id.ShouldBe(candidate.Id);
        summary.ReviewStatus.ShouldBe("flagged");
        summary.Notes.ShouldBe("Keep this note.");
        summary.CvDocumentCount.ShouldBe(1);

        var persisted = await dbContext.Candidates.SingleAsync(item => item.Id == candidate.Id);
        persisted.IntakeRoundId.ShouldBe(activeRound.Id);
        persisted.PromotedFromRoundNumber.ShouldBe(sourceRound.RoundNumber);
        persisted.PromotedAt.ShouldBe(FirstPromotionAt);
        persisted.ReviewStatus.ShouldBe(CandidateReviewStatus.Flagged);
        persisted.HireOutcome.ShouldBe(CandidateHireOutcome.None);
        persisted.RequirementReviews.ShouldHaveSingleItem();
        sourceRound.Candidates.ShouldBeEmpty();
        activeRound.Candidates.ShouldHaveSingleItem().Id.ShouldBe(candidate.Id);
    }

    [Fact]
    public async Task Handle_Should_MoveNothing_WhenSelectionContainsANonPromotableCandidate() // domain: Promote is atomic
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, sourceRound, activeRound) = await CreateRoundsAsync(dbContext);
        var flaggedCandidate = CandidateTestData.CreateCandidate(sourceRound.Id, 1);
        var rejectedCandidate = CandidateTestData.CreateCandidate(sourceRound.Id, 2);
        dbContext.Candidates.AddRange(flaggedCandidate, rejectedCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        flaggedCandidate.ApplyReview(CandidateReviewStatus.Flagged, null).IsSuccess.ShouldBeTrue();
        rejectedCandidate.ApplyReview(CandidateReviewStatus.Rejected, null).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(
                vacancy.Id,
                activeRound.Id,
                sourceRound.Id,
                [flaggedCandidate.Id, rejectedCandidate.Id]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors["candidateIds"]
            .ShouldContain(message => message.Contains(rejectedCandidate.Id.ToString()));

        var persisted = await dbContext.Candidates
            .OrderBy(item => item.Id)
            .ToListAsync();
        persisted.Select(item => item.IntakeRoundId).ShouldBe([sourceRound.Id, sourceRound.Id]);
        persisted.All(item => item.PromotedAt is null).ShouldBeTrue();
        activeRound.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPromoteAfterClearingDeclinedOutcome_WhenOutcomeIsNoLongerSet() // domain: Declined is dynamic promotion exclusion
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, sourceRound, activeRound) = await CreateRoundsAsync(dbContext);
        var candidate = CandidateTestData.CreateCandidate(sourceRound.Id);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, null).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.Declined).IsSuccess.ShouldBeTrue();
        candidate.SetHireOutcome(CandidateHireOutcome.None).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(
                vacancy.Id,
                activeRound.Id,
                sourceRound.Id,
                [candidate.Id]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.Candidates.SingleAsync()).IntakeRoundId.ShouldBe(activeRound.Id);
    }

    [Fact]
    public async Task Handle_ShouldOverwritePromotionProvenance_WhenCandidateIsPromotedAgain() // domain: Promote provenance records the latest hop
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, sourceRound, secondRound) = await CreateRoundsAsync(dbContext);
        var candidate = CandidateTestData.CreateCandidate(sourceRound.Id);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var firstHandler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var firstResult = await firstHandler.Handle(
            new PromoteCandidatesCommand(
                vacancy.Id,
                secondRound.Id,
                sourceRound.Id,
                [candidate.Id]),
            CancellationToken.None);
        firstResult.IsSuccess.ShouldBeTrue();

        var secondPromotionAt = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);
        vacancy.CloseRound(secondRound.Id, secondPromotionAt).IsSuccess.ShouldBeTrue();
        var thirdRoundResult = vacancy.CreateRound("Third wave");
        thirdRoundResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var thirdRound = thirdRoundResult.Value;
        var secondHandler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero)));
        var secondResult = await secondHandler.Handle(
            new PromoteCandidatesCommand(
                vacancy.Id,
                thirdRound.Id,
                secondRound.Id,
                [candidate.Id]),
            CancellationToken.None);

        secondResult.IsSuccess.ShouldBeTrue();
        var persisted = await dbContext.Candidates.SingleAsync();
        persisted.IntakeRoundId.ShouldBe(thirdRound.Id);
        persisted.PromotedFromRoundNumber.ShouldBe(secondRound.RoundNumber);
        persisted.PromotedAt.ShouldBe(new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Handle_ShouldReturnNoActiveRound_WhenOnlyRoundIsClosed() // domain: Active Round
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var round = vacancy.Rounds.Single();
        vacancy.CloseRound(round.Id, FirstPromotionAt).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(vacancy.Id, round.Id, 999, [1]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntakeRoundErrors.NoActiveRound(vacancy.Id));
    }

    [Fact]
    public async Task Handle_ShouldReturnNotClosed_WhenSourceRoundIsOpen() // domain: Round Closure
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var round = vacancy.Rounds.Single();
        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(vacancy.Id, round.Id, round.Id, [1]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntakeRoundErrors.NotClosed(round.Id));
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenVacancyIsClosed() // domain: Closed Vacancy
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy(closed: true);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new PromoteCandidatesCommandHandler(
            dbContext,
            new FixedTimeProvider(FirstPromotionAt));

        var result = await handler.Handle(
            new PromoteCandidatesCommand(vacancy.Id, vacancy.Rounds.Single().Id, 999, [1]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VacancyErrors.Closed(vacancy.Id));
    }

    private static async Task<(Vacancy Vacancy, IntakeRound SourceRound, IntakeRound ActiveRound)> CreateRoundsAsync(
        TestDbContext dbContext)
    {
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var sourceRound = vacancy.Rounds.Single();
        vacancy.CloseRound(sourceRound.Id, FirstPromotionAt).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var activeRoundResult = vacancy.CreateRound("Second wave");
        activeRoundResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (vacancy, sourceRound, activeRoundResult.Value);
    }
}