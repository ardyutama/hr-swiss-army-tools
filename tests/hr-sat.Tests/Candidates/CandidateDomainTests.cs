using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateDomainTests
{
    [Fact]
    public void Import_Should_ReturnValidationError_WhenCandidateHasNoCvDocuments() // domain: a candidate has at least one CV document
    {
        var result = Candidate.Import(
            1,
            "Candidate Applicant",
            "candidate@example.com",
            "Candidate application",
            "Please find my CV attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            "candidate.eml",
            "source-emails/candidate.eml",
            100,
            new byte[32],
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            []);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("Candidates.Invalid");
        error.Errors["documents"].ShouldContain("At least one CV document is required.");
    }
}