using hr_sat.Application.Abstractions.Storage;
using hr_sat.Application.Features.Candidates.Delete;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using NSubstitute;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class DeleteCandidateHandlerTests
{
    [Fact]
    public async Task Handle_Should_DeleteCandidateDocumentsAndFiles_WhenCandidateExists() // US-14: HR removes a candidate from an open vacancy
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var fileStorage = Substitute.For<IPrivateFileStorage>();
        fileStorage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new DeleteCandidateCommandHandler(dbContext, fileStorage);

        var result = await handler.Handle(
            new DeleteCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.Candidates.AnyAsync()).ShouldBeFalse();
        (await dbContext.CvDocuments.AnyAsync()).ShouldBeFalse();
        await fileStorage.Received(1).DeleteAsync(candidate.SourceStorageKey, CancellationToken.None);
        await fileStorage.Received(1).DeleteAsync(document.StorageKey, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // US-14: HR cannot remove a candidate from an unknown vacancy
    {
        await using var dbContext = new TestDbContext();
        var fileStorage = Substitute.For<IPrivateFileStorage>();
        var handler = new DeleteCandidateCommandHandler(dbContext, fileStorage);

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
            Substitute.For<IPrivateFileStorage>());

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
            Substitute.For<IPrivateFileStorage>());

        var result = await handler.Handle(
            new DeleteCandidateCommand(vacancy.Id, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CandidateErrors.NotFound(999));
    }
}