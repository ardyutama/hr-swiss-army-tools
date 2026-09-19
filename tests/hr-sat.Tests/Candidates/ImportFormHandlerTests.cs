using System.Text;
using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ImportFormHandlerTests
{
    [Fact]
    public async Task Handle_Should_CreateFormCandidateWithOrderedRawCells_WhenCsvContainsMultilineCells() // US-01: HR imports a Google Forms response while retaining the raw row
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"2026-09-16T10:00:00Z\",\"Alice\nApplicant\",\" Alice@EXAMPLE.com \""),
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        result.RowsRead.ShouldBe(1);
        result.Created.ShouldBe(1);
        result.Updated.ShouldBe(0);
        result.SkippedOutdated.ShouldBe(0);

        var candidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .SingleAsync();
        candidate.IntakeSource.ShouldBe(CandidateIntakeSource.Form);
        candidate.SourceOriginalFilename.ShouldBeNull();
        candidate.SourceStorageKey.ShouldBeNull();
        candidate.SourceSha256.ShouldBeNull();
        candidate.IsResubmitted.ShouldBeFalse();

        var response = candidate.FormResponses.ShouldHaveSingleItem();
        response.Cells.ShouldBe([
            "2026-09-16T10:00:00Z",
            "Alice\nApplicant",
            " Alice@EXAMPLE.com "]);
        response.IdentityKey.ShouldBe("alice@example.com");
        response.IsCurrent.ShouldBeTrue();
        response.FormTimestampRaw.ShouldBe("2026-09-16T10:00:00Z");
    }

    [Fact]
    public async Task Handle_Should_PrefillBoundRolesAndStampProvenance_WhenFormCandidateIsImported()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyWithLayoutAsync(
            dbContext,
            ["Timestamp", "Applicant", "Email", "Phone"],
            [
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, FormLayoutRole.ContactPhone, null)
            ]);

        await ImportAsync(
            dbContext,
            vacancy,
            FormCsvWithHeaders(
                ["Timestamp", "Applicant", "Email", "Phone"],
                "\"2026-09-16T10:00:00Z\",\" Alice Applicant \",\" alice@example.com \",\" +62 812 \""),
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        var candidate = await dbContext.Candidates.SingleAsync();
        candidate.FullName.ShouldBe("Alice Applicant");
        candidate.ContactEmail.ShouldBe("alice@example.com");
        candidate.ContactPhone.ShouldBe("+62 812");
        candidate.FullNameProvenance.ShouldBe(CandidateDetailProvenance.FormPrefilled);
        candidate.ContactEmailProvenance.ShouldBe(CandidateDetailProvenance.FormPrefilled);
        candidate.ContactPhoneProvenance.ShouldBe(CandidateDetailProvenance.FormPrefilled);
    }

    [Fact]
    public async Task Handle_Should_LeaveInvalidPrefillFieldsEmpty_AndStillImportTheRow()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyWithLayoutAsync(
            dbContext,
            ["Timestamp", "Applicant", "Email", "Phone"],
            [
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, FormLayoutRole.ContactPhone, null)
            ]);
        var tooLongName = new string('n', 301);
        var tooLongPhone = new string('1', 101);

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsvWithHeaders(
                ["Timestamp", "Applicant", "Email", "Phone"],
                $"\"2026-09-16T10:00:00Z\",\"{tooLongName}\",\"person@example.com\",\"{tooLongPhone}\""),
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        result.Created.ShouldBe(1);
        var candidate = await dbContext.Candidates.SingleAsync();
        candidate.FullName.ShouldBeNull();
        candidate.ContactEmail.ShouldBe("person@example.com");
        candidate.ContactPhone.ShouldBeNull();
        candidate.FullNameProvenance.ShouldBe(CandidateDetailProvenance.None);
        candidate.ContactEmailProvenance.ShouldBe(CandidateDetailProvenance.FormPrefilled);
        candidate.ContactPhoneProvenance.ShouldBe(CandidateDetailProvenance.None);
    }

    [Fact]
    public async Task Handle_Should_KeepTypedDetails_WhenFreshFormResponseIsReuploaded()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyWithLayoutAsync(
            dbContext,
            ["Timestamp", "Applicant", "Email", "Phone"],
            [
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, FormLayoutRole.ContactPhone, null)
            ]);
        string[] headers = ["Timestamp", "Applicant", "Email", "Phone"];
        await ImportAsync(
            dbContext,
            vacancy,
            FormCsvWithHeaders(
                headers,
                "\"2026-09-16T10:00:00Z\",\"Form Name\",\"form@example.com\",\"111\""),
            new DateTimeOffset(2026, 9, 16, 10, 30, 0, TimeSpan.Zero));

        var candidate = await dbContext.Candidates.SingleAsync();
        candidate.UpdateDetails("Typed Name", "typed@example.com", "999")
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsvWithHeaders(
                headers,
                "\"2026-09-16T11:00:00Z\",\"New Form Name\",\"form@example.com\",\"222\""),
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        result.Updated.ShouldBe(1);
        candidate.FullName.ShouldBe("Typed Name");
        candidate.ContactEmail.ShouldBe("typed@example.com");
        candidate.ContactPhone.ShouldBe("999");
        candidate.FullNameProvenance.ShouldBe(CandidateDetailProvenance.Typed);
        candidate.ContactEmailProvenance.ShouldBe(CandidateDetailProvenance.Typed);
        candidate.ContactPhoneProvenance.ShouldBe(CandidateDetailProvenance.Typed);
    }

    [Fact]
    public async Task Handle_Should_RetainTheLatestRowAndPriorResponse_WhenAnIdentityIsDuplicatedInOneFile() // domain: Form Response keeps the latest Timestamp and retains earlier submissions
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"2026-09-16T10:00:00Z\",\"First\",\"person@example.com\"",
                "\"2026-09-16T11:00:00Z\",\"Latest\",\"PERSON@example.com\""),
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

        result.RowsRead.ShouldBe(2);
        result.Created.ShouldBe(1);
        result.Updated.ShouldBe(0);
        result.SkippedOutdated.ShouldBe(0);

        var candidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .SingleAsync();
        candidate.FormResponses.Count.ShouldBe(2);
        candidate.FormResponses.Single(response => response.IsCurrent)
            .Cells[1]
            .ShouldBe("Latest");
        candidate.FormResponses.Single(response => !response.IsCurrent)
            .Cells[1]
            .ShouldBe("First");
    }

    [Fact]
    public async Task Handle_Should_UpdateOnlyFormResponseState_WhenAReuploadIsFresher() // domain: Resubmitted never changes review data or typed candidate details
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-16T10:00:00Z\",\"Original\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 10, 30, 0, TimeSpan.Zero));

        var candidate = await dbContext.Candidates.SingleAsync();
        candidate.UpdateDetails("Typed Name", "typed@example.com").IsSuccess.ShouldBeTrue();
        candidate.UpdateNotes("Review note").IsSuccess.ShouldBeTrue();
        candidate.ApplyReview(CandidateReviewStatus.Flagged, "Review note").IsSuccess.ShouldBeTrue();
        candidate.SetRequirementReview(vacancy.Requirements.Single().Id, true)
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"2026-09-16T11:00:00Z\",\"Middle\",\"PERSON@example.com\"",
                "\"2026-09-16T12:00:00Z\",\"Newest\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero));

        result.RowsRead.ShouldBe(2);
        result.Created.ShouldBe(0);
        result.Updated.ShouldBe(1);
        result.SkippedOutdated.ShouldBe(0);

        candidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .Include(item => item.RequirementReviews)
            .SingleAsync();
        candidate.IsResubmitted.ShouldBeTrue();
        candidate.FullName.ShouldBe("Typed Name");
        candidate.ContactEmail.ShouldBe("typed@example.com");
        candidate.Notes.ShouldBe("Review note");
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.Flagged);
        candidate.RequirementReviews.ShouldHaveSingleItem().Confirmed.ShouldBeTrue();
        candidate.FormResponses.Count.ShouldBe(3);
        candidate.FormResponses.Single(response => response.IsCurrent)
            .Cells[1]
            .ShouldBe("Newest");
    }

    [Fact]
    public async Task Handle_Should_SkipAnOlderReupload_WithoutMarkingCandidateResubmitted() // domain: an older Form Response cannot replace the current response
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-16T10:00:00Z\",\"Current\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 10, 30, 0, TimeSpan.Zero));

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-16T09:00:00Z\",\"Older\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero));

        result.RowsRead.ShouldBe(1);
        result.Created.ShouldBe(0);
        result.Updated.ShouldBe(0);
        result.SkippedOutdated.ShouldBe(1);

        var candidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .SingleAsync();
        candidate.IsResubmitted.ShouldBeFalse();
        candidate.FormResponses.ShouldHaveSingleItem().Cells[1].ShouldBe("Current");
    }

    [Fact]
    public async Task Handle_Should_UseTheLaterUnparseableRow_WhenAParsedRowIsOlderThanCurrent() // domain: unparseable timestamp rows use later file order without reviving an older response
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-16T10:00:00Z\",\"Current\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 10, 30, 0, TimeSpan.Zero));

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"not-a-timestamp\",\"Unparseable\",\"person@example.com\"",
                "\"2026-09-15T10:00:00Z\",\"Older parsed\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero));

        result.Updated.ShouldBe(1);
        result.SkippedOutdated.ShouldBe(1);
        var candidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .SingleAsync();
        candidate.FormResponses.Single(response => response.IsCurrent)
            .Cells[1]
            .ShouldBe("Unparseable");
    }

    [Fact]
    public async Task Handle_Should_DedupePhoneKeysAndCreateNoKeyRowsIndependently() // domain: Form Response identity falls back to phone and no-key rows are always new
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var firstImport = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"2026-09-16T10:00:00Z\",\"Phone applicant\",\"+62 (812) 3456-7890\"",
                "\"2026-09-16T10:01:00Z\",\"No key\",\"No phone here\""),
            new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero));

        firstImport.Created.ShouldBe(2);
        var phoneCandidate = await dbContext.Candidates
            .Include(item => item.FormResponses)
            .SingleAsync(item => item.FormResponses.Single().IdentityKey == "6281234567890");

        var secondImport = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv(
                "\"2026-09-16T12:00:00Z\",\"Phone applicant updated\",\"6281234567890\"",
                "\"2026-09-16T12:01:00Z\",\"Another no key\",\"Still no phone\""),
            new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero));

        secondImport.Created.ShouldBe(1);
        secondImport.Updated.ShouldBe(1);
        phoneCandidate.IsResubmitted.ShouldBeTrue();
        (await dbContext.Candidates.CountAsync()).ShouldBe(3);
        (await dbContext.Candidates
                .CountAsync(candidate => candidate.FormResponses.Single().IdentityKey == null))
            .ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_CreateInActiveRoundAndNoticePriorApplication_WhenKeyExistsOnlyInClosedRound() // domain: Prior Application Notice spans rounds within one vacancy
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var closedRound = vacancy.Rounds.Single();
        await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-16T10:00:00Z\",\"Prior\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 16, 10, 30, 0, TimeSpan.Zero));
        vacancy.CloseRound(
                closedRound.Id,
                new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero))
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var activeRoundResult = vacancy.CreateRound("Second wave");
        activeRoundResult.IsSuccess.ShouldBeTrue();
        var activeRound = activeRoundResult.Value;
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var result = await ImportAsync(
            dbContext,
            vacancy,
            FormCsv("\"2026-09-17T10:00:00Z\",\"Current\",\"person@example.com\""),
            new DateTimeOffset(2026, 9, 17, 11, 0, 0, TimeSpan.Zero),
            activeRound.Id);

        result.Created.ShouldBe(1);
        result.PriorApplications.ShouldBe(1);
        (await dbContext.Candidates.CountAsync()).ShouldBe(2);
        (await dbContext.Candidates.CountAsync(candidate => candidate.IntakeRoundId == closedRound.Id))
            .ShouldBe(1);
        (await dbContext.Candidates.CountAsync(candidate => candidate.IntakeRoundId == activeRound.Id))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenTargetRoundIsClosed() // domain: a closed Intake Round is read-only
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var round = vacancy.Rounds.Single();
        vacancy.CloseRound(round.Id, new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero))
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(dbContext, new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(FormCsv(
            "\"2026-09-16T10:00:00Z\",\"Closed\",\"person@example.com\"")));

        var result = await handler.Handle(
            new ImportFormCommand(
                vacancy.Id,
                round.Id,
                new ImportFormFile("responses.csv", "text/csv", content.Length, content)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("IntakeRounds.Closed");
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationErrorNamingTheRow_WhenCsvIsRagged() // domain: malformed Form Response imports are rejected atomically
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, DateTimeOffset.UtcNow);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            "Timestamp,Name,Email\r\n\"2026-09-16T10:00:00Z\",\"Missing email\"\r\n"));

        var result = await handler.Handle(
            new ImportFormCommand(
                vacancy.Id,
                vacancy.Rounds.Single().Id,
                new ImportFormFile("responses.csv", "text/csv", content.Length, content)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["file"]
            .ShouldContain(message => message.Contains("row 2", StringComparison.OrdinalIgnoreCase));
        (await dbContext.Candidates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_WhenCsvHasNoDataRows() // domain: an empty Form Response export is invalid
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await AddVacancyAsync(dbContext);
        var handler = CreateHandler(dbContext, DateTimeOffset.UtcNow);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("Timestamp,Name,Email\r\n"));

        var result = await handler.Handle(
            new ImportFormCommand(
                vacancy.Id,
                vacancy.Rounds.Single().Id,
                new ImportFormFile("responses.csv", "text/csv", content.Length, content)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["file"]
            .ShouldContain("The CSV must contain at least one data row.");
        (await dbContext.Candidates.CountAsync()).ShouldBe(0);
    }

    private static ImportFormCommandHandler CreateHandler(
        TestDbContext dbContext,
        DateTimeOffset importedAt) =>
        new(dbContext, new FixedTimeProvider(importedAt));

    private static async Task<Vacancy> AddVacancyAsync(TestDbContext dbContext)
        => await AddVacancyWithLayoutAsync(
            dbContext,
            ["Timestamp", "Name", "Email"],
            [
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
            ]);

    private static async Task<Vacancy> AddVacancyWithLayoutAsync(
        TestDbContext dbContext,
        IReadOnlyList<string> headers,
        IReadOnlyList<FormLayoutColumn> columns)
    {
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var layoutResult = FormLayout.Create(
            vacancy.Id,
            headers,
            new FormLayoutDefinition(columns));
        layoutResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(layoutResult.Value);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return vacancy;
    }

    private static async Task<ImportFormResponse> ImportAsync(
        TestDbContext dbContext,
        Vacancy vacancy,
        string csv,
        DateTimeOffset importedAt,
        long? roundId = null)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        await using var content = new MemoryStream(bytes);
        var result = await CreateHandler(dbContext, importedAt).Handle(
            new ImportFormCommand(
                vacancy.Id,
                roundId ?? vacancy.Rounds.Single(round => round.IsOpen).Id,
                new ImportFormFile("responses.csv", "text/csv", bytes.Length, content)),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string FormCsv(params string[] rows) =>
        FormCsvWithHeaders(["Timestamp", "Name", "Email"], rows);

    private static string FormCsvWithHeaders(
        IReadOnlyList<string> headers,
        params string[] rows) =>
        $"{string.Join(",", headers)}\r\n{string.Join("\r\n", rows)}\r\n";
}