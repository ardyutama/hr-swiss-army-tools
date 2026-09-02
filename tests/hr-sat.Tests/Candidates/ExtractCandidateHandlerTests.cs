using hr_sat.Application.Features.Candidates.Extract;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ExtractCandidateHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistExtractedDetails_WhenPrimaryCvHasText() // US-18: HR reviews details extracted from the primary CV
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var storage = new TestFileStorage();
        storage.Add(candidate.CvDocuments.Single().StorageKey, [1]);
        var handler = new ExtractCandidateCommandHandler(
            dbContext,
            storage,
            CandidateTestData.CreateExtractionService(
                "Name: Extracted Applicant\nEmail: extracted@example.com\nPhone: +1 555 123 4567\nSkills: SQL, C#"));

        var result = await handler.Handle(
            new ExtractCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ExtractionStatus.ShouldBe("succeeded");
        result.Value.FullName.ShouldBe("Extracted Applicant");
        result.Value.ContactEmail.ShouldBe("extracted@example.com");
        result.Value.ContactPhone.ShouldBe("+1 555 123 4567");
        result.Value.Skills.Select(skill => skill.Phrase).ShouldBe(new[] { "SQL", "C#" });
        candidate.ExtractionStatus.ShouldBe(CandidateExtractionStatus.Succeeded);
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.New);
    }

    [Fact]
    public async Task Handle_ShouldRetainCandidateAndMarkExtractionFailed_WhenPrimaryCvHasNoText() // US-18: unreadable CV extraction retains the candidate for review
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var storage = new TestFileStorage();
        storage.Add(candidate.CvDocuments.Single().StorageKey, [1]);
        var handler = new ExtractCandidateCommandHandler(
            dbContext,
            storage,
            CandidateTestData.CreateExtractionService());

        var result = await handler.Handle(
            new ExtractCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ExtractionStatus.ShouldBe("failed");
        result.Value.Documents.ShouldHaveSingleItem();
        result.Value.ReviewStatus.ShouldBe("new");
        (await dbContext.Candidates.CountAsync()).ShouldBe(1);
        (await dbContext.CvDocuments.CountAsync()).ShouldBe(1);
        candidate.ExtractionStatus.ShouldBe(CandidateExtractionStatus.Failed);
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.New);
    }

    [Fact]
    public async Task Handle_ShouldLeaveExtractionPending_WhenCandidateHasNoPrimaryCv() // domain: only the explicit Primary CV is eligible for extraction
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var candidate = CreateCandidateWithTwoDocuments(vacancy.Id);
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new ExtractCandidateCommandHandler(
            dbContext,
            new TestFileStorage(),
            CandidateTestData.CreateExtractionService("Name: Should Not Be Used"));

        var result = await handler.Handle(
            new ExtractCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ExtractionStatus.ShouldBe("pending");
        result.Value.FullName.ShouldBeNull();
        result.Value.Documents.ShouldAllBe(document => !document.IsPrimary);
        candidate.ExtractionStatus.ShouldBe(CandidateExtractionStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldRejectExtraction_WhenVacancyIsClosed() // domain: a Closed Vacancy is read-only
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext, closed: true);
        var handler = new ExtractCandidateCommandHandler(
            dbContext,
            new TestFileStorage(),
            CandidateTestData.CreateExtractionService("Name: Should Not Be Used"));

        var result = await handler.Handle(
            new ExtractCandidateCommand(vacancy.Id, candidate.Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<hr_sat.Domain.ValidationError>();
        error.Errors["status"].ShouldContain(
            "A closed vacancy must be reopened before candidate data can be changed.");
        candidate.ExtractionStatus.ShouldBe(CandidateExtractionStatus.Pending);
    }

    private static Candidate CreateCandidateWithTwoDocuments(long vacancyId)
    {
        var sourceHash = new byte[32];
        sourceHash[0] = 2;
        var result = Candidate.Import(
            vacancyId,
            "Multiple Document Applicant",
            "multiple@example.com",
            "Multiple document application",
            "Two CV documents are attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            "multiple.eml",
            "source-emails/multiple.eml",
            100,
            sourceHash,
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            [
                new StoredCvDocument(
                    "first.pdf",
                    "cv-documents/first.pdf",
                    1,
                    false,
                    20,
                    sourceHash),
                new StoredCvDocument(
                    "second.pdf",
                    "cv-documents/second.pdf",
                    2,
                    false,
                    20,
                    sourceHash)
            ]);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
