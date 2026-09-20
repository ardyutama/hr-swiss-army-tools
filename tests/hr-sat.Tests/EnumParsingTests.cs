using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests;

public sealed class EnumParsingTests
{
    [Fact]
    public void Domain_enum_parsing_matches_defined_member_names_case_insensitively()
    {
        EnumParsing.TryParseDefined<CandidateHireOutcome>(" HIRED ", out var parsed)
            .ShouldBeTrue();

        parsed.ShouldBe(CandidateHireOutcome.Hired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("not-an-outcome")]
    public void Domain_enum_parsing_rejects_values_that_are_not_member_names(string? value)
    {
        EnumParsing.TryParseDefined<CandidateHireOutcome>(value, out _)
            .ShouldBeFalse();
    }
}