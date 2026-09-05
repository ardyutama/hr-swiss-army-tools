using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateDomainTests
{
    [Fact]
    public void Import_Should_Succeed_WhenCandidateHasNoCvDocuments() // US-17: HR can review an email-only candidate
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

        result.IsSuccess.ShouldBeTrue();
        result.Value.CvDocuments.ShouldBeEmpty();
    }
}