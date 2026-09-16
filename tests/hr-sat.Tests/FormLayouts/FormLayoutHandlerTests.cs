using hr_sat.Application.Features.FormLayouts.Upsert;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutHandlerTests
{
    [Fact]
    public async Task Handle_Should_CreateAndReplaceFormLayout_WhenVacancyIsOpen() // domain: Form Layout is vacancy-owned ordinal mapping
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertFormLayoutCommandHandler(dbContext);

        var createResult = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                ["Timestamp", "Name", "Email"],
                1,
                2,
                null,
                null),
            CancellationToken.None);

        createResult.IsSuccess.ShouldBeTrue();
        createResult.Value.IsValid.ShouldBeTrue();
        createResult.Value.HeaderSnapshot.ShouldBe(["Timestamp", "Name", "Email"]);
        var layoutId = createResult.Value.Id;

        var replaceResult = await handler.Handle(
            new UpsertFormLayoutCommand(
                vacancy.Id,
                ["Timestamp", "Email", "Name"],
                2,
                1,
                null,
                null),
            CancellationToken.None);

        replaceResult.IsSuccess.ShouldBeTrue();
        replaceResult.Value.Id.ShouldBe(layoutId);
        replaceResult.Value.NameColumnOrdinal.ShouldBe(2);
        replaceResult.Value.ContactEmailColumnOrdinal.ShouldBe(1);
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
                ["Timestamp", "Name", "Email"],
                1,
                2,
                null,
                null),
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
                ["Timestamp", "Name", "Email"],
                1,
                2,
                null,
                null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Vacancies.Invalid");
        error.Errors["status"].ShouldHaveSingleItem()
            .ShouldBe("A closed vacancy must be reopened before its Form Layout can be changed.");
        (await dbContext.FormLayouts.CountAsync()).ShouldBe(0);
    }
}
