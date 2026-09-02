using hr_sat.Application.Features.Candidates.Delete;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class DeleteCandidateHandlerTests
{
    [Fact]
    public async Task Handle_Should_DeleteCandidateDocumentsAndEnqueueFileDeletions_WhenCandidateExists() // US-14: HR removes a candidate from an open vacancy
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var handler = new DeleteCandidateCommandHandler(
            dbContext,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new DeleteCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.Candidates.AnyAsync()).ShouldBeFalse();
        (await dbContext.CvDocuments.AnyAsync()).ShouldBeFalse();
        var pendingKeys = await dbContext.PendingFileDeletions
            .Select(deletion => deletion.StorageKey)
            .ToListAsync();
        pendingKeys.ShouldBe([candidate.SourceStorageKey, document.StorageKey], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // US-14: HR cannot remove a candidate from an unknown vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = new DeleteCandidateCommandHandler(
            dbContext,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(
            new DeleteCandidateCommand(999, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CandidateErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_WhenVacancyIsClosed() // domain: closed vacancy is read-only
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, _) = await CandidateTestData.SeedCandidateAsync(dbContext, closed: true);
        var handler = new DeleteCandidateCommandHandler(
            dbContext,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(
            new DeleteCandidateCommand(vacancy.Id, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Vacancies.Invalid");
        error.Errors["status"].ShouldContain(
            "A closed vacancy must be reopened before candidates can be removed.");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCandidateDoesNotBelongToVacancy() // US-14: HR cannot remove an unknown candidate
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new DeleteCandidateCommandHandler(
            dbContext,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(
            new DeleteCandidateCommand(vacancy.Id, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CandidateErrors.NotFound(999));
    }
}