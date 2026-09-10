using hr_sat.Application.Features.Candidates.PromoteCandidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class PromoteCandidatesValidatorsTests
{
    [Fact]
    public void Promote_candidates_should_require_a_non_empty_selection() // domain: Promote
    {
        var result = new PromoteCandidatesCommandValidator().Validate(
            new PromoteCandidatesCommand(1, 2, 1, []));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(PromoteCandidatesCommand.CandidateIds));
    }

    [Fact]
    public void Promote_candidates_should_reject_the_active_round_as_source() // domain: Promote
    {
        var result = new PromoteCandidatesCommandValidator().Validate(
            new PromoteCandidatesCommand(1, 2, 2, [3]));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(PromoteCandidatesCommand.SourceRoundId));
    }

    [Fact]
    public void Promote_candidates_should_accept_positive_ids_and_duplicate_selection() // domain: Promote duplicate selections are deduplicated
    {
        var result = new PromoteCandidatesCommandValidator().Validate(
            new PromoteCandidatesCommand(1, 2, 1, [3, 3]));

        result.IsValid.ShouldBeTrue();
    }
}