using System.Text;
using hr_sat.Application.Features.Candidates.Import;
using hr_sat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ImportCandidatesHandlerTests
{
    [Fact]
    public async Task Handle_Should_ImportCandidateAndDocuments_WhenVacancyIsOpen() // US-12/US-13: HR imports a source email with its CV document
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var storage = new TestFileStorage();
        var sourceBytes = CreateEml();
        using var content = new MemoryStream(sourceBytes);
        var handler = new ImportCandidatesCommandHandler(
            dbContext,
            storage,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ImportFilePreparer>.Instance,
            CandidateTestData.CreateExtractionService());

        var result = await handler.Handle(
            new ImportCandidatesCommand(
                vacancy.Id,
                [new ImportCandidateFile(
                    "candidate.eml",
                    "message/rfc822",
                    sourceBytes.Length,
                    content)]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value.Results.ShouldHaveSingleItem();
        outcome.Status.ShouldBe("imported");
        outcome.Candidate.ShouldNotBeNull();
        outcome.Candidate.SourceSenderEmail.ShouldBe("candidate@example.com");
        outcome.Candidate.Documents.ShouldHaveSingleItem();
        (await dbContext.Candidates.CountAsync()).ShouldBe(1);
        (await dbContext.CvDocuments.CountAsync()).ShouldBe(1);
        storage.Files.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // US-12: HR cannot import into an unknown vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = new ImportCandidatesCommandHandler(
            dbContext,
            new TestFileStorage(),
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<ImportFilePreparer>.Instance,
            CandidateTestData.CreateExtractionService());

        var result = await handler.Handle(
            new ImportCandidatesCommand(999, []),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(hr_sat.Domain.Candidates.CandidateErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_WhenVacancyIsClosed() // domain: closed vacancy is read-only and cannot receive candidate imports
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy(closed: true);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new ImportCandidatesCommandHandler(
            dbContext,
            new TestFileStorage(),
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<ImportFilePreparer>.Instance,
            CandidateTestData.CreateExtractionService());

        var result = await handler.Handle(
            new ImportCandidatesCommand(vacancy.Id, []),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Vacancies.Invalid");
        error.Errors["status"].ShouldContain(
            "A closed vacancy cannot receive candidate imports.");
    }

    private static byte[] CreateEml()
    {
        const string boundary = "hr-sat-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var builder = new StringBuilder();
        builder.Append("From: Candidate Applicant <candidate@example.com>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append("Subject: Candidate application\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n");
        builder.Append("\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        builder.Append("Please find my CV attached.\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: application/pdf; name=\"candidate.pdf\"\r\n");
        builder.Append("Content-Disposition: attachment; filename=\"candidate.pdf\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        builder.Append(Convert.ToBase64String(pdf));
        builder.Append($"\r\n--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}