using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Application.Features.Candidates.PriorApplications;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ImportFormPlanTests
{
    private const string Key = "person@example.com";

    [Fact]
    public void Build_Should_PlanASingletonCreateWithoutPriorNotice_WhenRowHasNoIdentityKey() // domain: no-key rows are always new candidates
    {
        var plan = FormImportPlan.Build(
            [Row(1, null, Timestamp(10))],
            EmptyCurrent,
            Lookup("unrelated@example.com", Match(candidateId: 99)));

        var create = plan.Groups.ShouldHaveSingleItem().ShouldBeOfType<CreateFormCandidatePlan>();
        create.RowsInMutationOrder.Select(row => row.RowPosition).ShouldBe([1]);
        plan.Created.ShouldBe(1);
        plan.Updated.ShouldBe(0);
        plan.SkippedOutdated.ShouldBe(0);
        plan.PriorApplications.ShouldBe(0);
    }

    [Fact]
    public void Build_Should_AppendTheLatestRowLast_WhenAnIdentityIsDuplicatedInOneFile() // domain: Form Response keeps the latest Timestamp and retains earlier submissions
    {
        var plan = FormImportPlan.Build(
            [
                Row(1, Key, Timestamp(10)),
                Row(2, Key, Timestamp(12)),
                Row(3, Key, Timestamp(11))
            ],
            EmptyCurrent,
            EmptyLookup);

        var create = plan.Groups.ShouldHaveSingleItem().ShouldBeOfType<CreateFormCandidatePlan>();
        create.RowsInMutationOrder.Select(row => row.RowPosition).ShouldBe([1, 3, 2]);
        plan.Created.ShouldBe(1);
    }

    [Fact]
    public void Build_Should_TreatEqualTimestampsAsFresh_AndLetTheLaterPositionedRowWin() // domain: duplicate resolution uses >= on the form Timestamp
    {
        var createPlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(10)), Row(2, Key, Timestamp(10))],
            EmptyCurrent,
            EmptyLookup);
        var create = createPlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<CreateFormCandidatePlan>();
        create.RowsInMutationOrder.Select(row => row.RowPosition).ShouldBe([1, 2]);

        var updatePlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(10))],
            Current(Key, candidateId: 7, Timestamp(10)),
            EmptyLookup);
        updatePlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<UpdateFormCandidatePlan>();
        updatePlan.Updated.ShouldBe(1);
        updatePlan.SkippedOutdated.ShouldBe(0);
    }

    [Fact]
    public void Build_Should_TreatUnparseableTimestampsAsLater() // domain: unparseable timestamp rows win by later file order
    {
        var createPlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(10)), Row(2, Key, null)],
            EmptyCurrent,
            EmptyLookup);
        var create = createPlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<CreateFormCandidatePlan>();
        create.RowsInMutationOrder.Select(row => row.RowPosition).ShouldBe([1, 2]);

        var updatePlan = FormImportPlan.Build(
            [Row(1, Key, null)],
            Current(Key, candidateId: 7, Timestamp(10)),
            EmptyLookup);
        updatePlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<UpdateFormCandidatePlan>();
        updatePlan.Updated.ShouldBe(1);
    }

    [Fact]
    public void Build_Should_SkipTheGroup_WhenNoRowIsFresherThanTheCurrentResponse() // domain: an older Form Response cannot replace the current response
    {
        var plan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(10)), Row(2, Key, Timestamp(11))],
            Current(Key, candidateId: 7, Timestamp(12)),
            Lookup(Key, Match(candidateId: 7)));

        plan.Groups.ShouldBeEmpty();
        plan.Created.ShouldBe(0);
        plan.Updated.ShouldBe(0);
        plan.SkippedOutdated.ShouldBe(2);
        plan.PriorApplications.ShouldBe(0);
    }

    [Fact]
    public void Build_Should_PlanAnUpdateWithOnlyTheFreshRows_WhenSomeRowsAreStale() // domain: Resubmitted keeps only fresher rows
    {
        var plan = FormImportPlan.Build(
            [
                Row(1, Key, Timestamp(10)),
                Row(2, Key, Timestamp(11)),
                Row(3, Key, Timestamp(12))
            ],
            Current(Key, candidateId: 7, Timestamp(11)),
            EmptyLookup);

        var update = plan.Groups.ShouldHaveSingleItem().ShouldBeOfType<UpdateFormCandidatePlan>();
        update.IdentityKey.ShouldBe(Key);
        update.RowsInMutationOrder.Select(row => row.RowPosition).ShouldBe([2, 3]);
        plan.Created.ShouldBe(0);
        plan.Updated.ShouldBe(1);
        plan.SkippedOutdated.ShouldBe(1);
    }

    [Fact]
    public void Build_Should_FlagPriorApplication_ByLookupPresenceForCreates_AndByOtherCandidatesForUpdates() // domain: Prior Application Notice
    {
        var createPlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(10))],
            EmptyCurrent,
            Lookup(Key, Match(candidateId: 99)));
        createPlan.PriorApplications.ShouldBe(1);
        createPlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<CreateFormCandidatePlan>();

        var ownCandidateOnlyPlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(12))],
            Current(Key, candidateId: 7, Timestamp(10)),
            Lookup(Key, Match(candidateId: 7)));
        ownCandidateOnlyPlan.PriorApplications.ShouldBe(0);
        ownCandidateOnlyPlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<UpdateFormCandidatePlan>();

        var otherCandidatePlan = FormImportPlan.Build(
            [Row(1, Key, Timestamp(12))],
            Current(Key, candidateId: 7, Timestamp(10)),
            Lookup(Key, Match(candidateId: 7), Match(candidateId: 42)));
        otherCandidatePlan.PriorApplications.ShouldBe(1);
        otherCandidatePlan.Groups.ShouldHaveSingleItem().ShouldBeOfType<UpdateFormCandidatePlan>();
    }

    private static readonly IReadOnlyDictionary<string, CurrentFormResponseState> EmptyCurrent =
        new Dictionary<string, CurrentFormResponseState>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PriorApplicationMatch>> EmptyLookup =
        new Dictionary<string, IReadOnlyList<PriorApplicationMatch>>(StringComparer.Ordinal);

    private static ParsedFormRow Row(int position, string? identityKey, DateTimeOffset? timestamp) =>
        new(
            position,
            [timestamp?.ToString("O") ?? "not-a-timestamp", "Applicant", identityKey ?? "no key"],
            timestamp?.ToString("O") ?? "not-a-timestamp",
            timestamp,
            identityKey);

    private static DateTimeOffset Timestamp(int hour) =>
        new(2026, 9, 16, hour, 0, 0, TimeSpan.Zero);

    private static IReadOnlyDictionary<string, CurrentFormResponseState> Current(
        string identityKey,
        long candidateId,
        DateTimeOffset? formTimestampParsed) =>
        new Dictionary<string, CurrentFormResponseState>(StringComparer.Ordinal)
        {
            [identityKey] = new CurrentFormResponseState(candidateId, formTimestampParsed)
        };

    private static PriorApplicationMatch Match(long candidateId) =>
        new(
            candidateId,
            RoundNumber: 1,
            RoundName: null,
            ReviewStatus: "new",
            ImportedAt: new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero));

    private static IReadOnlyDictionary<string, IReadOnlyList<PriorApplicationMatch>> Lookup(
        string identityKey,
        params PriorApplicationMatch[] matches) =>
        new Dictionary<string, IReadOnlyList<PriorApplicationMatch>>(StringComparer.Ordinal)
        {
            [identityKey] = matches
        };
}
