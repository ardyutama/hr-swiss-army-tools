using hr_sat.Application.Features.Candidates.SelectPrimaryCv;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class SelectPrimaryCvHandlerTests
{
    [Fact]
    public async Task Handle_ShouldSelectOnlyRequestedDocumentAndResetExtraction() // domain: the Primary CV is explicit and selecting it resets derived extraction state
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var candidate = CreateCandidateWithTwoDocuments(vacancy.Id);
        candidate.ApplyExtraction(new CandidateExtraction(
            "Previously Extracted",
            "previous@example.com",
            "+1 555 000 0000",
            ["SQL"])).IsSuccess.ShouldBeTrue();
        dbContext.Candidates.Add(candidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var requestedDocument = candidate.CvDocuments.Single(document => document.Position == 2);
        var handler = new SelectPrimaryCvCommandHandler(dbContext);

        var result = await handler.Handle(
            new SelectPrimaryCvCommand(vacancy.Id, candidate.Id, requestedDocument.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ExtractionStatus.ShouldBe("pending");
        result.Value.FullName.ShouldBeNull();
        result.Value.ContactEmail.ShouldBeNull();
        result.Value.Skills.ShouldBeEmpty();
        result.Value.Documents[0].IsPrimary.ShouldBeFalse();
        result.Value.Documents[1].IsPrimary.ShouldBeTrue();
        result.Value.ReviewStatus.ShouldBe("new");

        var documents = await dbContext.CvDocuments
            .AsNoTracking()
            .Where(document => document.CandidateId == candidate.Id)
            .OrderBy(document => document.Position)
            .ToListAsync();
        documents.Count(document => document.IsPrimary).ShouldBe(1);
        documents.Single(document => document.Position == 2).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRejectDocumentFromAnotherCandidate() // domain: a CV document belongs to exactly one candidate
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var firstCandidate = CreateCandidateWithTwoDocuments(vacancy.Id);
        var secondCandidate = CandidateTestData.CreateCandidate(vacancy.Id, sourceNumber: 3);
        dbContext.Candidates.AddRange(firstCandidate, secondCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new SelectPrimaryCvCommandHandler(dbContext);

        var result = await handler.Handle(
            new SelectPrimaryCvCommand(
                vacancy.Id,
                firstCandidate.Id,
                secondCandidate.CvDocuments.Single().Id),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<hr_sat.Domain.ValidationError>();
        error.Errors["documentId"].ShouldContain(
            "The CV document does not belong to this candidate.");
        firstCandidate.CvDocuments.Count(document => document.IsPrimary).ShouldBe(1);
    }

    private static Candidate CreateCandidateWithTwoDocuments(long vacancyId)
    {
        var sourceHash = new byte[32];
        sourceHash[0] = 4;
        var result = Candidate.Import(
            vacancyId,
            "Primary Selection Applicant",
            "primary@example.com",
            "Primary selection application",
            "Two CV documents are attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            "primary.eml",
            "source-emails/primary.eml",
            100,
            sourceHash,
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            [
                new StoredCvDocument(
                    "first.pdf",
                    "cv-documents/primary-first.pdf",
                    1,
                    true,
                    20,
                    sourceHash),
                new StoredCvDocument(
                    "second.pdf",
                    "cv-documents/primary-second.pdf",
                    2,
                    false,
                    20,
                    sourceHash)
            ]);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
