using hr_sat.Application.Features.ScreeningRules;
using hr_sat.Application.Features.ScreeningRules.Preview;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class PreviewScreeningRulesQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnSnapshotRequired_WhenFormLayoutDoesNotExist() // domain: Screening Rules require a valid Form Layout
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new PreviewScreeningRulesQueryHandler(dbContext);

        var result = await handler.Handle(
            new PreviewScreeningRulesQuery(
                vacancy.Id,
                [new ScreeningRule(1, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ScreeningRuleSetErrors.SnapshotRequired(vacancy.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalid_WhenRuleDoesNotReferenceTheFormSnapshot() // domain: Screening Rules reference form columns by ordinal
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await SeedVacancyWithLayoutAsync(dbContext);
        var handler = new PreviewScreeningRulesQueryHandler(dbContext);

        var result = await handler.Handle(
            new PreviewScreeningRulesQuery(
                vacancy.Id,
                [new ScreeningRule(4, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("ScreeningRules.Invalid");
        error.Errors.ShouldContainKey("rules[0].ordinal");
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPreview_WhenVacancyHasNoActiveRound() // domain: an Active Round may legally be absent between waves
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await SeedVacancyWithLayoutAsync(dbContext);
        var round = vacancy.Rounds.Single();
        vacancy.CloseRound(
                round.Id,
                new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero))
            .IsSuccess
            .ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new PreviewScreeningRulesQueryHandler(dbContext);

        var result = await handler.Handle(
            new PreviewScreeningRulesQuery(
                vacancy.Id,
                [new ScreeningRule(3, ScreeningOperator.Equals, "Yes")]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Total.ShouldBe(0);
        result.Value.ScreenedOut.ShouldBe(0);
        result.Value.PerRule.ShouldHaveSingleItem()
            .ShouldBe(new ScreeningRulePreviewResponse(0, 0));
    }

    [Fact]
    public async Task Handle_Should_CountOnlyActiveFormCandidatesAndEachFiredRule() // domain: Screening Rules never screen Source Email candidates
    {
        await using var dbContext = new TestDbContext();
        var vacancy = await SeedVacancyWithLayoutAsync(dbContext);
        var round = vacancy.Rounds.Single();
        var noCandidate = AddFormCandidate(vacancy, round, "No", 1);
        var yesCandidate = AddFormCandidate(vacancy, round, "Yes", 2);
        var emailCandidate = CandidateTestData.CreateCandidate(round.Id, sourceNumber: 3);
        dbContext.Candidates.AddRange(noCandidate, yesCandidate, emailCandidate);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new PreviewScreeningRulesQueryHandler(dbContext);

        var result = await handler.Handle(
            new PreviewScreeningRulesQuery(
                vacancy.Id,
                [new ScreeningRule(3, ScreeningOperator.Equals, "no")]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Total.ShouldBe(2);
        result.Value.ScreenedOut.ShouldBe(1);
        result.Value.PerRule.ShouldHaveSingleItem()
            .ShouldBe(new ScreeningRulePreviewResponse(0, 1));
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

    private static Candidate AddFormCandidate(
        Vacancy vacancy,
        IntakeRound round,
        string availability,
        int sourceNumber)
    {
        var importedAt = new DateTimeOffset(
            2026,
            9,
            19,
            10 + sourceNumber,
            0,
            0,
            TimeSpan.Zero);
        var candidateResult = vacancy.ImportFormCandidate(
            new CandidateFormImportData(round.Id, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var candidate = candidateResult.Value;
        var responseResult = candidate.AddFormResponse(
            new CandidateFormResponseData(
                [
                    $"2026-09-19T{10 + sourceNumber:00}:00:00Z",
                    $"Candidate {sourceNumber}",
                    $"candidate{sourceNumber}@example.com",
                    availability
                ],
                $"2026-09-19T{10 + sourceNumber:00}:00:00Z",
                importedAt,
                $"candidate{sourceNumber}@example.com",
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        return candidate;
    }
}