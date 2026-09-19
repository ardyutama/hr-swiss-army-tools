using hr_sat.Application.Features.FormLayouts;
using hr_sat.Application.Features.FormLayouts.Upsert;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReplaceExistingFormLayout_WhenVacancyIsOpen() // domain: Form Layout is vacancy-owned ordinal mapping
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var layoutResult = FormLayout.Create(
            vacancy.Id,
            ["Timestamp", "Name", "Email"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
            ]));
        layoutResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(layoutResult.Value);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertFormLayoutCommandHandler(dbContext);

        var replaceResult = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                [
                    new FormLayoutColumn(1, FormLayoutRole.ContactEmail, "Email address"),
                    new FormLayoutColumn(2, FormLayoutRole.Name, "Applicant")
                ]),
            CancellationToken.None);

        replaceResult.IsSuccess.ShouldBeTrue();
        replaceResult.Value.Id.ShouldBe(layoutResult.Value.Id);
        replaceResult.Value.HeaderSnapshot.ShouldBe(["Timestamp", "Name", "Email"]);
        replaceResult.Value.Columns.ShouldBe([
            new FormLayoutColumnResponse(1, "contactemail", "Email address"),
            new FormLayoutColumnResponse(2, "name", "Applicant")
        ]);
        vacancy.DomainEvents.ShouldContain(item => item is FormLayoutUpsertedDomainEvent);
        (await dbContext.FormLayouts.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // domain: Form Layout belongs to one vacancy
    {
        await using var dbContext = new TestDbContext();
        var handler = new UpsertFormLayoutCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertFormLayoutCommand(
                999,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, null),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VacancyErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_RefuseChanges_WhenVacancyIsClosed() // domain: closed Vacancy is read-only
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy(closed: true);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertFormLayoutCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, null),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Vacancies.Invalid");
        error.Errors["status"].ShouldHaveSingleItem()
            .ShouldBe("A closed vacancy must be reopened before its Form Layout can be changed.");
        (await dbContext.FormLayouts.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public void Create_Should_RejectMissingRequiredRoles()
    {
        var result = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null)
            ]));

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldContainKey("contactEmail");
    }

    [Fact]
    public void Create_Should_RejectDuplicateOrdinalsAndRoles()
    {
        var duplicateOrdinals = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(1, FormLayoutRole.ContactEmail, null)
            ]));
        var duplicateRoles = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email", "Other"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.Name, null),
                new FormLayoutColumn(3, FormLayoutRole.ContactEmail, null)
            ]));

        duplicateOrdinals.IsFailure.ShouldBeTrue();
        duplicateOrdinals.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldContainKey("ordinals");
        duplicateRoles.IsFailure.ShouldBeTrue();
        duplicateRoles.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldContainKey("roles");
    }

    [Fact]
    public void Create_Should_RejectTimestampAndOutOfRangeOrdinals()
    {
        var result = FormLayout.Create(
            1,
            ["Timestamp", "Name", "Email"],
            new FormLayoutDefinition([
                new FormLayoutColumn(0, FormLayoutRole.Name, null),
                new FormLayoutColumn(3, FormLayoutRole.ContactEmail, null)
            ]));

        result.IsFailure.ShouldBeTrue();
        var errors = result.Error.ShouldBeOfType<ValidationError>().Errors;
        errors.Keys.ShouldContain("columns[0]");
        errors.Keys.ShouldContain("columns[3]");
    }

    [Fact]
    public void Create_Should_RejectMoreThanEightPickedColumns()
    {
        var columns = Enumerable.Range(1, 9)
            .Select(ordinal => new FormLayoutColumn(
                ordinal,
                ordinal switch
                {
                    1 => FormLayoutRole.Name,
                    2 => FormLayoutRole.ContactEmail,
                    _ => null
                },
                null))
            .ToArray();
        var headers = new[] { "Timestamp" }
            .Concat(Enumerable.Range(1, 9).Select(ordinal => $"Column {ordinal}"))
            .ToArray();

        var result = FormLayout.Create(1, headers, new FormLayoutDefinition(columns));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldContainKey(nameof(FormLayoutDefinition.Columns));
    }

    [Fact]
    public async Task Handle_Should_TrimLabels_ClearBlanks_AndDiscardUnpickedColumns()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var layoutResult = FormLayout.Create(
            vacancy.Id,
            ["Timestamp", "Name", "Email", "Phone"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, "  Candidate  "),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, "Email"),
                new FormLayoutColumn(3, FormLayoutRole.ContactPhone, "Phone")
            ]));
        layoutResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(layoutResult.Value);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var result = await new UpsertFormLayoutCommandHandler(dbContext).Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, "  Applicant  "),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, " ")
                ]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Columns.ShouldBe([
            new FormLayoutColumnResponse(1, "name", "Applicant"),
            new FormLayoutColumnResponse(2, "contactemail", null)
        ]);
    }

    [Fact]
    public async Task Handle_Should_ReprojectOnlyActiveRoundAndReportPrefillCounts()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        string[] headers = ["Timestamp", "Name", "Email", "New name", "New email"];
        var initialLayoutResult = FormLayout.Create(
            vacancy.Id,
            headers,
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
            ]));
        initialLayoutResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(initialLayoutResult.Value);

        var firstRound = vacancy.Rounds.Single();
        var firstCandidate = AddFormCandidate(
            vacancy,
            firstRound,
            initialLayoutResult.Value,
            ["2026-09-16T10:00:00Z", "Closed name", "closed@example.com", "Closed new", "closed-new@example.com"],
            new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero));
        dbContext.Candidates.Add(firstCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        vacancy.CloseRound(
                firstRound.Id,
                new DateTimeOffset(2026, 9, 17, 11, 0, 0, TimeSpan.Zero))
            .IsSuccess
            .ShouldBeTrue();
        var secondRoundResult = vacancy.CreateRound("Second wave");
        secondRoundResult.IsSuccess.ShouldBeTrue();
        var secondRound = secondRoundResult.Value;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var secondCandidate = AddFormCandidate(
            vacancy,
            secondRound,
            initialLayoutResult.Value,
            ["2026-09-18T10:00:00Z", "Active old", "active-old@example.com", "Active new", "active-new@example.com"],
            new DateTimeOffset(2026, 9, 18, 11, 0, 0, TimeSpan.Zero));
        dbContext.Candidates.Add(secondCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = new UpsertFormLayoutCommandHandler(dbContext);
        var reprojection = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                [
                    new FormLayoutColumn(3, FormLayoutRole.Name, null),
                    new FormLayoutColumn(4, FormLayoutRole.ContactEmail, null)
                ]),
            CancellationToken.None);

        reprojection.IsSuccess.ShouldBeTrue();
        reprojection.Value.CandidatesUpdated.ShouldBe(1);
        reprojection.Value.TypedOverridesKept.ShouldBe(0);
        firstCandidate.FullName.ShouldBe("Closed name");
        firstCandidate.ContactEmail.ShouldBe("closed@example.com");
        secondCandidate.FullName.ShouldBe("Active new");
        secondCandidate.ContactEmail.ShouldBe("active-new@example.com");

        secondCandidate.UpdateDetails(
                "Typed name",
                "typed@example.com",
                "typed phone")
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var typedReprojection = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, null),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]),
            CancellationToken.None);

        typedReprojection.IsSuccess.ShouldBeTrue();
        typedReprojection.Value.CandidatesUpdated.ShouldBe(0);
        typedReprojection.Value.TypedOverridesKept.ShouldBe(3);
        secondCandidate.FullName.ShouldBe("Typed name");
        secondCandidate.ContactEmail.ShouldBe("typed@example.com");
        secondCandidate.ContactPhone.ShouldBe("typed phone");
    }

    private static Candidate AddFormCandidate(
        Vacancy vacancy,
        IntakeRound round,
        FormLayout layout,
        IReadOnlyList<string> cells,
        DateTimeOffset importedAt)
    {
        var candidateResult = vacancy.ImportFormCandidate(
            new CandidateFormImportData(round.Id, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var candidate = candidateResult.Value;
        var responseResult = candidate.AddFormResponse(
            new CandidateFormResponseData(
                cells,
                cells[0],
                importedAt,
                "person@example.com",
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        candidate.PrefillDetailsFromLayout(layout);
        return candidate;
    }
}
