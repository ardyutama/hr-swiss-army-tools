using hr_sat.Application.Features.ScreeningRules;
using hr_sat.Application.Features.ScreeningRules.Upsert;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class UpsertScreeningRulesHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenVacancyDoesNotExist() // domain: Screening Rules are vacancy-owned
    {
        await using var dbContext = new TestDbContext();
        var handler = new UpsertScreeningRulesCommandHandler(dbContext);
        var command = new UpsertScreeningRulesCommand(
            999,
            [new ScreeningRule(1, ScreeningOperator.Equals, "Yes")]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VacancyErrors.NotFound(command.VacancyId));
    }

    [Fact]
    public async Task Handle_Should_ReturnSnapshotRequired_WhenFormLayoutDoesNotExist() // domain: Screening Rules require a valid Form Layout
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertScreeningRulesCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertScreeningRulesCommand(
                vacancy.Id,
                [new ScreeningRule(1, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ScreeningRuleSetErrors.SnapshotRequired(vacancy.Id));
        (await dbContext.ScreeningRuleSets.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RefuseChanges_WhenVacancyIsClosed() // domain: closed Vacancy is read-only
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy(closed: true);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertScreeningRulesCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertScreeningRulesCommand(
                vacancy.Id,
                [new ScreeningRule(1, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Vacancies.Invalid");
        error.Errors["status"].ShouldHaveSingleItem()
            .ShouldBe("A closed vacancy must be reopened before its Screening Rules can be changed.");
        (await dbContext.ScreeningRuleSets.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalid_WhenRuleDoesNotReferenceTheFormSnapshot() // domain: Screening Rules reference form columns by ordinal
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await SeedVacancyWithLayoutAsync(dbContext);
        var handler = new UpsertScreeningRulesCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertScreeningRulesCommand(
                vacancy.Id,
                [new ScreeningRule(4, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("ScreeningRules.Invalid");
        error.Errors.ShouldContainKey("rules[0].ordinal");
        (await dbContext.ScreeningRuleSets.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_PersistRulesAndRaiseDomainEvent_WhenVacancyIsOpen() // domain: Screening Rules are vacancy-owned and live
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await SeedVacancyWithLayoutAsync(dbContext);
        var handler = new UpsertScreeningRulesCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertScreeningRulesCommand(
                vacancy.Id,
                [new ScreeningRule(3, ScreeningOperator.Equals, "  Yes  ")]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VacancyId.ShouldBe(vacancy.Id);
        result.Value.Rules.ShouldBe([
            new ScreeningRuleResponse(3, "equals", "Yes")
        ]);
        var persisted = await dbContext.ScreeningRuleSets.SingleAsync();
        persisted.VacancyId.ShouldBe(vacancy.Id);
        persisted.Rules.ShouldBe([
            new ScreeningRule(3, ScreeningOperator.Equals, "Yes")
        ]);
        vacancy.DomainEvents.ShouldContain(item => item is ScreeningRuleSetUpsertedDomainEvent);
    }

    private static async Task<Vacancy> SeedVacancyWithLayoutAsync(TestDbContext dbContext)
    {
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var layoutResult = FormLayout.Create(
            vacancy.Id,
            ["Timestamp", "Name", "Email", "Availability"],
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, null, "Availability")
            ]));
        layoutResult.IsSuccess.ShouldBeTrue();
        dbContext.FormLayouts.Add(layoutResult.Value);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return vacancy;
    }
}