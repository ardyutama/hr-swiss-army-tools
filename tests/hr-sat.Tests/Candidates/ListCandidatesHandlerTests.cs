using hr_sat.Application.Features.Candidates.List;
using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using NSubstitute;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ListCandidatesHandlerTests
{
    [Fact]
    public async Task Handle_Should_AssembleReaderRowsWithoutLoadingCandidateGraphs_WhenRoundExists() // US-14: HR sees candidate summaries in the candidate list
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var reader = Substitute.For<ICandidateListReader>();
        reader.ReadAsync(Arg.Any<CandidateListReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CandidateListReadResult(
                [new CandidateListReadRow(
                    42,
                    "Reader Name",
                    "reader@example.com",
                    null,
                    "Reader note",
                    "flagged",
                    "none",
                    "Sender Name",
                    "sender@example.com",
                    "Reader subject",
                    new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero),
                    2,
                    "form",
                    true,
                    true,
                    [new ScreeningRuleMatch(0, "Availability · equals \"no\"")])],
                1,
                1,
                new CandidateListReadCounts(0, 1, 0, 0, 1, 0, 0, 0, 0, 1)));

        var handler = new ListCandidatesQueryHandler(dbContext, reader);

        var result = await handler.Handle(
            new ListCandidatesQuery(vacancy.Id, vacancy.Rounds.Single().Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var item = result.Value.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(42);
        item.FullName.ShouldBe("Reader Name");
        item.CvDocumentCount.ShouldBe(2);
        item.ScreenedOut.ShouldBeTrue();
        item.FiredRules.ShouldHaveSingleItem().Display.ShouldBe(
            "Availability · equals \"no\"");
        await reader.Received(1).ReadAsync(
            Arg.Is<CandidateListReadRequest>(request =>
                request.RoundId == vacancy.Rounds.Single().Id &&
                request.PageSize == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // US-14: HR cannot review candidates for an unknown vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = new ListCandidatesQueryHandler(
            dbContext,
            Substitute.For<ICandidateListReader>());

        var result = await handler.Handle(
            new ListCandidatesQuery(999, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CandidateErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_ReturnRoundNotFound_WhenRoundDoesNotBelongToVacancy()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new ListCandidatesQueryHandler(
            dbContext,
            Substitute.For<ICandidateListReader>());

        var result = await handler.Handle(
            new ListCandidatesQuery(vacancy.Id, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntakeRoundErrors.NotFound(999));
    }
}