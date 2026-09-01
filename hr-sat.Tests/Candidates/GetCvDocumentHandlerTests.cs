using hr_sat.Application.Abstractions.Storage;
using hr_sat.Application.Features.Candidates.GetCvDocument;
using NSubstitute;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class GetCvDocumentHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnThePdfStream_WhenDocumentBelongsToCandidate() // US-14: HR opens a candidate's CV document
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var content = new MemoryStream("%PDF-1.7"u8.ToArray());
        var fileStorage = Substitute.For<IPrivateFileStorage>();
        fileStorage.OpenReadAsync(document.StorageKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(content));
        var handler = new GetCvDocumentQueryHandler(dbContext, fileStorage);

        var result = await handler.Handle(
            new GetCvDocumentQuery(vacancy.Id, candidate.Id, document.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldBeSameAs(content);
        result.Value.ContentType.ShouldBe("application/pdf");
        result.Value.FileName.ShouldBe(document.OriginalFilename);
        await result.Value.Content.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDocumentDoesNotBelongToVacancy() // US-14: HR cannot open a CV outside the vacancy pipeline
    {
        await using var dbContext = new TestDbContext();
        var handler = new GetCvDocumentQueryHandler(
            dbContext,
            Substitute.For<IPrivateFileStorage>());

        var result = await handler.Handle(
            new GetCvDocumentQuery(999, 999, 999),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(hr_sat.Domain.Candidates.CandidateErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenStoredPdfIsMissing() // US-14: an unavailable CV is reported as not found
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var fileStorage = Substitute.For<IPrivateFileStorage>();
        fileStorage.OpenReadAsync(document.StorageKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new FileNotFoundException()));
        var handler = new GetCvDocumentQueryHandler(dbContext, fileStorage);

        var result = await handler.Handle(
            new GetCvDocumentQuery(vacancy.Id, candidate.Id, document.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(hr_sat.Domain.Candidates.CandidateErrors.NotFound(document.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenStoredPdfDirectoryIsMissing() // US-14: an unavailable CV is reported as not found
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var fileStorage = Substitute.For<IPrivateFileStorage>();
        fileStorage.OpenReadAsync(document.StorageKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new DirectoryNotFoundException()));
        var handler = new GetCvDocumentQueryHandler(dbContext, fileStorage);

        var result = await handler.Handle(
            new GetCvDocumentQuery(vacancy.Id, candidate.Id, document.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(hr_sat.Domain.Candidates.CandidateErrors.NotFound(document.Id));
    }
}