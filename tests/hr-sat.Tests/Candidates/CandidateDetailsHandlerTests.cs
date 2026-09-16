using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Application.Features.Candidates;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateDetailsHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnPriorApplicationsInDescendingRoundOrder_WhenSameSenderAppliedInClosedRounds() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, firstRound, secondRound, currentRound) =
            await CreateThreeRoundsAsync(dbContext);
        var firstCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            firstRound.Id,
            "person@example.com",
            1,
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero));
        firstCandidate.ApplyReview(CandidateReviewStatus.Rejected, null).IsSuccess.ShouldBeTrue();
        var secondCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            secondRound.Id,
            "PERSON@example.com",
            2,
            new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero));
        secondCandidate.ApplyReview(CandidateReviewStatus.Shortlisted, null).IsSuccess.ShouldBeTrue();
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            "person@example.com",
            3,
            new DateTimeOffset(2026, 9, 10, 11, 0, 0, TimeSpan.Zero));
        dbContext.Candidates.AddRange(firstCandidate, secondCandidate, currentCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, currentRound, currentCandidate);

        details.PriorApplications.ShouldBe([
            new CandidatePriorApplicationResponse(2, "July wave", "shortlisted"),
            new CandidatePriorApplicationResponse(1, null, "rejected")
        ]);
    }

    [Fact]
    public async Task Handle_ShouldMatchTrimmedCaseInsensitiveSenderEmails_WhenApplicationsUseDifferentFormatting() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, priorRound, currentRound) = await CreateTwoRoundsAsync(dbContext);
        var priorCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            priorRound.Id,
            "  PERSON@example.com  ",
            1);
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            "person@EXAMPLE.COM",
            2);
        dbContext.Candidates.AddRange(priorCandidate, currentCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, currentRound, currentCandidate);

        details.PriorApplications.ShouldHaveSingleItem().RoundNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ShouldIncludeMatchingCandidatesInTheSameRound() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, _, currentRound) = await CreateTwoRoundsAsync(dbContext);
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            "person@example.com",
            1);
        currentCandidate.ApplyReview(CandidateReviewStatus.Flagged, null).IsSuccess.ShouldBeTrue();
        var sameRoundCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            " PERSON@EXAMPLE.COM ",
            2,
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));
        dbContext.Candidates.AddRange(currentCandidate, sameRoundCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, currentRound, currentCandidate);

        details.PriorApplications.ShouldBe([
            new CandidatePriorApplicationResponse(2, "Current wave", "new")]);
    }

    [Fact]
    public async Task Handle_ShouldIncludeMatchingFormCandidatesInTheSameRound() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var round = vacancy.Rounds.Single();
        var currentCandidate = CreateFormCandidate(
            round.Id,
            "person@example.com",
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        currentCandidate.ApplyReview(CandidateReviewStatus.Flagged, null).IsSuccess.ShouldBeTrue();
        var sameRoundCandidate = CreateFormCandidate(
            round.Id,
            "person@example.com",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));
        dbContext.Candidates.AddRange(currentCandidate, sameRoundCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, round, currentCandidate);

        details.PriorApplications.ShouldBe([
            new CandidatePriorApplicationResponse(1, null, "new")]);
    }

    [Fact]
    public async Task Handle_ShouldReturnNoPriorApplications_WhenCurrentSenderEmailIsNull() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, priorRound, currentRound) = await CreateTwoRoundsAsync(dbContext);
        var priorCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            priorRound.Id,
            "person@example.com",
            1);
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            null,
            2);
        dbContext.Candidates.AddRange(priorCandidate, currentCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, currentRound, currentCandidate);

        details.PriorApplications.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldExcludeMatchingCandidatesFromAnotherVacancy() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var currentVacancy = CandidateTestData.CreateVacancy();
        var otherVacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.AddRange(currentVacancy, otherVacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var currentRound = currentVacancy.Rounds.Single();
        var otherRound = otherVacancy.Rounds.Single();
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            "person@example.com",
            1);
        var otherCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            otherRound.Id,
            "person@example.com",
            2);
        dbContext.Candidates.AddRange(currentCandidate, otherCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(
            dbContext,
            currentVacancy,
            currentRound,
            currentCandidate);

        details.PriorApplications.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldKeepLatestImportedCandidate_WhenSenderIsDuplicatedInAPriorRound() // domain: Prior Application Notice
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, priorRound, currentRound) = await CreateTwoRoundsAsync(dbContext);
        var earlierCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            priorRound.Id,
            "person@example.com",
            1,
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero));
        earlierCandidate.ApplyReview(CandidateReviewStatus.Rejected, null).IsSuccess.ShouldBeTrue();
        var latestCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            priorRound.Id,
            " PERSON@example.com ",
            2,
            new DateTimeOffset(2026, 8, 21, 11, 0, 0, TimeSpan.Zero));
        latestCandidate.ApplyReview(CandidateReviewStatus.Flagged, null).IsSuccess.ShouldBeTrue();
        var currentCandidate = CandidateTestData.CreateCandidateWithSenderEmail(
            currentRound.Id,
            "person@example.com",
            3);
        dbContext.Candidates.AddRange(earlierCandidate, latestCandidate, currentCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var details = await ReadDetailsAsync(dbContext, vacancy, currentRound, currentCandidate);

        var priorApplication = details.PriorApplications.ShouldHaveSingleItem();
        priorApplication.RoundNumber.ShouldBe(1);
        priorApplication.ReviewStatus.ShouldBe("flagged");
    }

    private static async Task<CandidateDetailsResponse> ReadDetailsAsync(
        TestDbContext dbContext,
        Vacancy vacancy,
        IntakeRound round,
        Candidate candidate)
    {
        var handler = new GetCandidateDetailsQueryHandler(dbContext);
        var result = await handler.Handle(
            new GetCandidateDetailsQuery(vacancy.Id, round.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static Candidate CreateFormCandidate(
        long roundId,
        string identityKey,
        DateTimeOffset importedAt)
    {
        var candidateResult = Candidate.ImportForm(new CandidateFormImportData(roundId, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var responseResult = candidateResult.Value.AddFormResponse(
            new CandidateFormResponseData(
                ["timestamp", "Applicant", identityKey],
                "2026-08-20T10:00:00Z",
                importedAt,
                identityKey,
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        return candidateResult.Value;
    }

    private static async Task<(Vacancy Vacancy, IntakeRound PriorRound, IntakeRound CurrentRound)> CreateTwoRoundsAsync(
        TestDbContext dbContext)
    {
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var priorRound = vacancy.Rounds.Single();
        vacancy.CloseRound(
            priorRound.Id,
            new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero))
            .IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var currentRoundResult = vacancy.CreateRound("Current wave");
        currentRoundResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (vacancy, priorRound, currentRoundResult.Value);
    }

    private static async Task<(
        Vacancy Vacancy,
        IntakeRound FirstRound,
        IntakeRound SecondRound,
        IntakeRound CurrentRound)> CreateThreeRoundsAsync(TestDbContext dbContext)
    {
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var firstRound = vacancy.Rounds.Single();
        vacancy.CloseRound(
            firstRound.Id,
            new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero))
            .IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var secondRoundResult = vacancy.CreateRound("July wave");
        secondRoundResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var secondRound = secondRoundResult.Value;
        vacancy.CloseRound(
            secondRound.Id,
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero))
            .IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var currentRoundResult = vacancy.CreateRound("Current wave");
        currentRoundResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (vacancy, firstRound, secondRound, currentRoundResult.Value);
    }
}
