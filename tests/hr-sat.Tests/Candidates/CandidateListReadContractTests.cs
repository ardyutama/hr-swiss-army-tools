using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace hr_sat.Tests.Candidates;

public abstract class CandidateListReadContractTests
{
    protected abstract Task<CandidateListReadHarness> CreateHarnessAsync();

    [Fact]
    public async Task US_14_candidate_list_read_applies_all_screening_operators_with_trim_case_and_empty_edges()
    {
        await using var harness = await CreateHarnessAsync();
        var cases = new[]
        {
            new OperatorCase(
                ScreeningOperator.Equals,
                "no",
                [" No ", "YES", "   "],
                1),
            new OperatorCase(
                ScreeningOperator.NotEquals,
                "no",
                [" YES ", "   ", ""],
                3),
            new OperatorCase(
                ScreeningOperator.IsEmpty,
                null,
                ["   ", "Yes", ""],
                2),
            new OperatorCase(
                ScreeningOperator.NotEmpty,
                null,
                [" Yes ", "   ", ""],
                1),
            new OperatorCase(
                ScreeningOperator.Contains,
                "yes",
                [" YES indeed ", "no", ""],
                1)
        };

        foreach (var testCase in cases)
        {
            var seed = await CandidateListContractData.CreateAsync(
                harness.DbContext,
                [new ScreeningRule(3, testCase.Operator, testCase.Value)]);
            foreach (var (cells, index) in testCase.Cells.Select((cells, index) => (cells, index)))
            {
                CandidateListContractData.AddFormCandidate(
                    harness.DbContext,
                    seed,
                    index + 1,
                    [
                        "Timestamp",
                        $"Applicant {index + 1}",
                        $"applicant{index + 1}@example.com",
                        cells]);
            }

            await harness.DbContext.SaveChangesAsync(CancellationToken.None);

            var result = await harness.Reader.ReadAsync(
                new CandidateListReadRequest(
                    seed.Round.Id,
                    RoundClosed: false,
                    Status: null,
                    Outcome: null,
                    Query: null,
                    Sort: null,
                    IncludeScreenedOut: true,
                    Page: 1,
                    PageSize: 100),
                CancellationToken.None);

            result.Total.ShouldBe(3);
            result.FilteredTotal.ShouldBe(3);
            result.Rows.Count(row => row.ScreenedOut).ShouldBe(testCase.ScreenedOutCount);
            result.Counts.ScreenedOut.ShouldBe(testCase.ScreenedOutCount);
            result.Rows
                .Where(row => row.ScreenedOut)
                .SelectMany(row => row.FiredRules)
                .ShouldAllBe(rule => rule.Index == 0);
        }
    }

    [Fact]
    public async Task US_14_candidate_list_read_keeps_screening_scope_and_counts_independent_of_filters()
    {
        await using var harness = await CreateHarnessAsync();
        var seed = await CandidateListContractData.CreateAsync(
            harness.DbContext,
            [new ScreeningRule(3, ScreeningOperator.Equals, "no")]);
        CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            1,
            ["Timestamp", "Screened", "screened@example.com", "No"]);
        var newCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            2,
            ["Timestamp", "Alpha", "alpha@example.com", "Yes"]);
        var flaggedCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            3,
            ["Timestamp", "Bravo", "bravo@example.com", "Yes"]);
        var undecidedCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            4,
            ["Timestamp", "Charlie", "charlie@example.com", "Yes"]);
        var hiredCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            5,
            ["Timestamp", "Diana", "diana@example.com", "Yes"]);
        var runawayCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            6,
            ["Timestamp", "Eve", "eve@example.com", "Yes"]);
        var declinedCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            7,
            ["Timestamp", "Fay", "fay@example.com", "Yes"]);
        var rejectedCandidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            8,
            ["Timestamp", "Grace", "grace@example.com", "Yes"]);
        CandidateListContractData.ApplyReview(flaggedCandidate, CandidateReviewStatus.Flagged);
        CandidateListContractData.ApplyReview(
            undecidedCandidate,
            CandidateReviewStatus.Shortlisted);
        CandidateListContractData.ApplyReview(
            hiredCandidate,
            CandidateReviewStatus.Shortlisted,
            CandidateHireOutcome.Hired);
        CandidateListContractData.ApplyReview(
            runawayCandidate,
            CandidateReviewStatus.Shortlisted,
            CandidateHireOutcome.Runaway);
        CandidateListContractData.ApplyReview(
            declinedCandidate,
            CandidateReviewStatus.Shortlisted,
            CandidateHireOutcome.Declined);
        CandidateListContractData.ApplyReview(rejectedCandidate, CandidateReviewStatus.Rejected);
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        var defaultResult = await harness.Reader.ReadAsync(
            Request(seed.Round.Id),
            CancellationToken.None);
        defaultResult.Total.ShouldBe(7);
        defaultResult.FilteredTotal.ShouldBe(7);
        defaultResult.Rows.Count.ShouldBe(7);
        defaultResult.Rows.Count(row => row.ScreenedOut).ShouldBe(0);
        defaultResult.Counts.New.ShouldBe(1);
        defaultResult.Counts.Flagged.ShouldBe(1);
        defaultResult.Counts.Shortlisted.ShouldBe(4);
        defaultResult.Counts.Rejected.ShouldBe(1);
        defaultResult.Counts.Any.ShouldBe(7);
        defaultResult.Counts.Undecided.ShouldBe(1);
        defaultResult.Counts.Hired.ShouldBe(1);
        defaultResult.Counts.Runaway.ShouldBe(1);
        defaultResult.Counts.Declined.ShouldBe(1);
        defaultResult.Counts.ScreenedOut.ShouldBe(1);

        var allResult = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, includeScreenedOut: true),
            CancellationToken.None);
        allResult.Total.ShouldBe(8);
        allResult.Rows.Count(row => row.ScreenedOut).ShouldBe(1);

        var flaggedResult = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, status: "FLAGGED"),
            CancellationToken.None);
        flaggedResult.FilteredTotal.ShouldBe(1);
        flaggedResult.Rows.ShouldHaveSingleItem().FullName.ShouldBe("Bravo");

        var hiredResult = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, outcome: "HIRED"),
            CancellationToken.None);
        hiredResult.FilteredTotal.ShouldBe(1);
        hiredResult.Rows.ShouldHaveSingleItem().FullName.ShouldBe("Diana");

        var queryResult = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, query: "  aLpHa  "),
            CancellationToken.None);
        queryResult.FilteredTotal.ShouldBe(1);
        queryResult.Rows.ShouldHaveSingleItem().ContactEmail.ShouldBe("alpha@example.com");
        newCandidate.Id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task US_14_candidate_list_read_query_matches_typed_contact_phone()
    {
        await using var harness = await CreateHarnessAsync();
        var seed = await CandidateListContractData.CreateAsync(harness.DbContext, []);
        var byPhone = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            1,
            ["Timestamp", "Phone Applicant", "phone@example.com", "Yes"]);
        byPhone.UpdateDetails("Phone Applicant", "phone@example.com", "+62 812-3456")
            .IsSuccess.ShouldBeTrue();
        CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            2,
            ["Timestamp", "Other Applicant", "other@example.com", "Yes"]);
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        var result = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, query: "812-3456"),
            CancellationToken.None);

        result.FilteredTotal.ShouldBe(1);
        result.Rows.ShouldHaveSingleItem().FullName.ShouldBe("Phone Applicant");
    }

    [Fact]
    public async Task US_14_candidate_list_read_orders_both_directions_and_pages_rows()
    {
        await using var harness = await CreateHarnessAsync();
        var seed = await CandidateListContractData.CreateAsync(harness.DbContext, []);
        var candidates = Enumerable.Range(1, 5)
            .Select(index => CandidateListContractData.AddEmailCandidate(
                harness.DbContext,
                seed,
                index,
                new DateTimeOffset(2026, 8, 20 + index, 12, 0, 0, TimeSpan.Zero)))
            .ToArray();
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        var newestPage = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, page: 1, pageSize: 2),
            CancellationToken.None);
        newestPage.Total.ShouldBe(5);
        newestPage.FilteredTotal.ShouldBe(5);
        newestPage.Rows.Select(row => row.Id).ShouldBe([candidates[4].Id, candidates[3].Id]);

        var newestSecondPage = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, page: 2, pageSize: 2),
            CancellationToken.None);
        newestSecondPage.Rows.Select(row => row.Id).ShouldBe([candidates[2].Id, candidates[1].Id]);

        var oldestPage = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, sort: "oldest", page: 1, pageSize: 2),
            CancellationToken.None);
        oldestPage.Rows.Select(row => row.Id).ShouldBe([candidates[0].Id, candidates[1].Id]);
    }

    [Fact]
    public async Task Domain_closed_round_read_uses_the_frozen_screening_verdict_after_rule_edits()
    {
        await using var harness = await CreateHarnessAsync();
        var seed = await CandidateListContractData.CreateAsync(
            harness.DbContext,
            [new ScreeningRule(3, ScreeningOperator.Equals, "no")]);
        var candidate = CandidateListContractData.AddFormCandidate(
            harness.DbContext,
            seed,
            1,
            ["Timestamp", "Frozen Applicant", "frozen@example.com", "No"]);
        seed.Round.AddCandidate(candidate);
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        seed.Vacancy.CloseRound(
            seed.Round.Id,
            new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero))
            .IsSuccess.ShouldBeTrue();
        seed.Vacancy.UpsertScreeningRules(
                new ScreeningRuleDefinition([
                    new ScreeningRule(3, ScreeningOperator.Equals, "yes")]))
            .IsSuccess.ShouldBeTrue();
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        var result = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, roundClosed: true, includeScreenedOut: true),
            CancellationToken.None);

        var row = result.Rows.ShouldHaveSingleItem();
        row.ScreenedOut.ShouldBeTrue();
        row.FiredRules.ShouldHaveSingleItem().Display.ShouldBe(
            "Availability · equals \"no\"");
    }

    [Fact]
    public async Task Domain_email_sourced_candidates_are_never_screened()
    {
        await using var harness = await CreateHarnessAsync();
        var seed = await CandidateListContractData.CreateAsync(
            harness.DbContext,
            [new ScreeningRule(3, ScreeningOperator.Equals, "no")]);
        CandidateListContractData.AddEmailCandidate(
            harness.DbContext,
            seed,
            1,
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        await harness.DbContext.SaveChangesAsync(CancellationToken.None);

        var result = await harness.Reader.ReadAsync(
            Request(seed.Round.Id, includeScreenedOut: true),
            CancellationToken.None);

        var row = result.Rows.ShouldHaveSingleItem();
        row.IntakeSource.ShouldBe("email");
        row.ScreenedOut.ShouldBeFalse();
        row.FiredRules.ShouldBeEmpty();
    }

    private static CandidateListReadRequest Request(
        long roundId,
        bool roundClosed = false,
        string? status = null,
        string? outcome = null,
        string? query = null,
        string? sort = null,
        bool includeScreenedOut = false,
        int page = 1,
        int pageSize = 100) =>
        new(
            roundId,
            roundClosed,
            status,
            outcome,
            query,
            sort,
            includeScreenedOut,
            page,
            pageSize);

    private sealed record OperatorCase(
        ScreeningOperator Operator,
        string? Value,
        IReadOnlyList<string> Cells,
        int ScreenedOutCount);
}

public sealed class EfCandidateListReadContractTests : CandidateListReadContractTests
{
    protected override Task<CandidateListReadHarness> CreateHarnessAsync() =>
        Task.FromResult<CandidateListReadHarness>(new SqliteCandidateListReadHarness());
}

public sealed class PostgresCandidateListReadContractTests(
    PostgresCandidateListReadContractFixture fixture)
    : CandidateListReadContractTests, IClassFixture<PostgresCandidateListReadContractFixture>
{
    protected override Task<CandidateListReadHarness> CreateHarnessAsync() =>
        fixture.CreateHarnessAsync();
}

public abstract class CandidateListReadHarness : IAsyncDisposable
{
    public abstract IApplicationDbContext DbContext { get; }
    public abstract ICandidateListReader Reader { get; }

    public abstract ValueTask DisposeAsync();
}

internal sealed class SqliteCandidateListReadHarness : CandidateListReadHarness
{
    private readonly TestDbContext dbContext = new();

    public override IApplicationDbContext DbContext => dbContext;
    public override ICandidateListReader Reader => reader;

    private EfCandidateListReader reader => new(dbContext);

    public override async ValueTask DisposeAsync() => await dbContext.DisposeAsync();
}

public sealed class PostgresCandidateListReadContractFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task<CandidateListReadHarness> CreateHarnessAsync()
    {
        var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE vacancy RESTART IDENTITY CASCADE");
        return new PostgresCandidateListReadHarness(dbContext);
    }

    public async Task DisposeAsync() => await postgres.DisposeAsync();

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}

internal sealed class PostgresCandidateListReadHarness(AppDbContext dbContext)
    : CandidateListReadHarness
{
    public override IApplicationDbContext DbContext => dbContext;
    public override ICandidateListReader Reader => reader;

    private PostgresCandidateListReader reader => new(dbContext);

    public override async ValueTask DisposeAsync() => await dbContext.DisposeAsync();
}

internal sealed record CandidateListSeed(
    Vacancy Vacancy,
    IntakeRound Round,
    FormLayout Layout,
    ScreeningRuleSet RuleSet);

internal static class CandidateListContractData
{
    public static async Task<CandidateListSeed> CreateAsync(
        IApplicationDbContext dbContext,
        IReadOnlyList<ScreeningRule> rules)
    {
        var vacancyResult = Vacancy.Create(
            "Data Analyst",
            new DateOnly(2026, 8, 20),
            ["SQL"],
            null);
        vacancyResult.IsSuccess.ShouldBeTrue();
        var vacancy = vacancyResult.Value;
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var layoutResult = vacancy.UpsertFormLayout(
            new FormLayoutDefinition([
                new FormLayoutColumn(1, FormLayoutRole.Name, null),
                new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null),
                new FormLayoutColumn(3, null, "Availability")]),
            ["Timestamp", "Name", "Email", "Availability"]);
        layoutResult.IsSuccess.ShouldBeTrue();
        var ruleSetResult = vacancy.UpsertScreeningRules(new ScreeningRuleDefinition(rules));
        ruleSetResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return new CandidateListSeed(
            vacancy,
            vacancy.Rounds.Single(),
            layoutResult.Value,
            ruleSetResult.Value);
    }

    public static Candidate AddFormCandidate(
        IApplicationDbContext dbContext,
        CandidateListSeed seed,
        int number,
        IReadOnlyList<string> cells)
    {
        var importedAt = new DateTimeOffset(2026, 8, 20, 10 + number % 10, 0, 0, TimeSpan.Zero);
        var candidateResult = Candidate.ImportForm(
            new CandidateFormImportData(seed.Round.Id, importedAt));
        candidateResult.IsSuccess.ShouldBeTrue();
        var candidate = candidateResult.Value;
        var responseResult = candidate.AddFormResponse(
            new CandidateFormResponseData(
                cells,
                cells[0],
                DateTimeOffset.Parse("2026-08-20T09:00:00+00:00"),
                cells.Count > 2 ? cells[2].ToLowerInvariant() : null,
                importedAt),
            currentResponse: null,
            isResubmitted: false);
        responseResult.IsSuccess.ShouldBeTrue();
        candidate.PrefillDetailsFromLayout(seed.Layout);
        dbContext.Candidates.Add(candidate);
        return candidate;
    }

    public static Candidate AddEmailCandidate(
        IApplicationDbContext dbContext,
        CandidateListSeed seed,
        int number,
        DateTimeOffset sourceSentAt)
    {
        var candidate = CandidateTestData.CreateCandidate(
            seed.Round.Id,
            number,
            sourceSentAt: sourceSentAt);
        dbContext.Candidates.Add(candidate);
        return candidate;
    }

    public static void ApplyReview(
        Candidate candidate,
        CandidateReviewStatus status,
        CandidateHireOutcome outcome = CandidateHireOutcome.None)
    {
        candidate.ApplyReview(status, null).IsSuccess.ShouldBeTrue();
        if (outcome != CandidateHireOutcome.None)
        {
            if (outcome == CandidateHireOutcome.Runaway)
            {
                candidate.SetHireOutcome(CandidateHireOutcome.Hired).IsSuccess.ShouldBeTrue();
            }

            candidate.SetHireOutcome(outcome).IsSuccess.ShouldBeTrue();
        }
    }
}