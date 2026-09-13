using hr_sat.Application.Features.IntakeRounds;
using hr_sat.Application.Features.IntakeRounds.Close;
using hr_sat.Application.Features.IntakeRounds.Create;
using hr_sat.Domain;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.IntakeRounds;

public sealed class IntakeRoundHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenCreatingWhileAnActiveRoundExists()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new CreateIntakeRoundCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateIntakeRoundCommand(vacancy.Id, "Second wave"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntakeRoundErrors.ActiveRoundExists(vacancy.Id));
    }

    [Fact]
    public async Task Handle_Should_CreateNextRound_WhenThePreviousRoundIsClosed()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        vacancy.CloseRound(
            vacancy.Rounds.Single().Id,
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new CreateIntakeRoundCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateIntakeRoundCommand(vacancy.Id, "  Second wave  "),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RoundNumber.ShouldBe(2);
        result.Value.Name.ShouldBe("Second wave");
        result.Value.Status.ShouldBe("open");
        result.Value.CandidateCount.ShouldBe(0);
        (await dbContext.IntakeRounds.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCreatingForAnUnknownVacancy()
    {
        await using var dbContext = new TestDbContext();
        var handler = new CreateIntakeRoundCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateIntakeRoundCommand(999, null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VacancyErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_CloseRoundAndReturnTimestamp_WhenRoundIsOpen()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var roundId = vacancy.Rounds.Single().Id;
        var closedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var handler = new CloseIntakeRoundCommandHandler(
            dbContext,
            new FixedTimeProvider(closedAt));

        var result = await handler.Handle(
            new CloseIntakeRoundCommand(vacancy.Id, roundId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(roundId);
        result.Value.Status.ShouldBe("closed");
        result.Value.ClosedAt.ShouldBe(closedAt);
        var persisted = await dbContext.IntakeRounds.SingleAsync();
        persisted.IsOpen.ShouldBeFalse();
        persisted.ClosedAt.ShouldBe(closedAt);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenClosingAnAlreadyClosedRound()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var roundId = vacancy.Rounds.Single().Id;
        vacancy.CloseRound(
            roundId,
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new CloseIntakeRoundCommandHandler(
            dbContext,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new CloseIntakeRoundCommand(vacancy.Id, roundId),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntakeRoundErrors.Closed(roundId));
    }
}
