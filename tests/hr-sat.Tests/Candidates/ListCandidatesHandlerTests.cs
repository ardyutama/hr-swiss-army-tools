using hr_sat.Application.Features.Candidates.List;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ListCandidatesHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnCandidatesInImportOrder_WhenVacancyExists() // US-14: HR reviews candidates in import order
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.Candidates.AddRange(
            CandidateTestData.CreateCandidate(
                vacancy.Id,
                2,
                new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero)),
            CandidateTestData.CreateCandidate(
                vacancy.Id,
                1,
                new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new ListCandidatesQueryHandler(dbContext);

        var result = await handler.Handle(
            new ListCandidatesQuery(vacancy.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(candidate => candidate.SourceSenderName)
            .ShouldBe(["Candidate 1", "Candidate 2"]);
    }

    [Fact]
    public async Task Handle_Should_IncludeCvDocumentCount_WhenVacancyExists() // US-14: HR sees at a glance whether a candidate has a CV
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, _) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var handler = new ListCandidatesQueryHandler(dbContext);

        var result = await handler.Handle(
            new ListCandidatesQuery(vacancy.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var candidate = result.Value.ShouldHaveSingleItem();
        candidate.CvDocumentCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // US-14: HR cannot review candidates for an unknown vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = new ListCandidatesQueryHandler(dbContext);

        var result = await handler.Handle(
            new ListCandidatesQuery(999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CandidateErrors.NotFound(999));
    }
}