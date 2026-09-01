using hr_sat.Application.Features.Candidates.Shared;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateExtractionTests
{
    [Fact]
    public void Parse_ShouldExtractCandidateDetailsAndInlineSkills() // US-14/US-18: HR reviews extracted candidate details and skills
    {
        var extraction = CandidateTextParser.Parse(
            """
            Name: Ada Lovelace
            Email: ada@example.com
            Date: 2026-08-29
            Phone: +1 (555) 123-4567
            Skills: SQL, C#, Docker
            Experience:
            Data analysis
            """);

        extraction.FullName.ShouldBe("Ada Lovelace");
        extraction.ContactEmail.ShouldBe("ada@example.com");
        extraction.ContactPhone.ShouldBe("+1 (555) 123-4567");
        extraction.Skills.ShouldBe(new[] { "SQL", "C#", "Docker" });
    }

    [Fact]
    public void Parse_ShouldExtractLabeledDetailsWhenPdfTextConcatenatesLines() // US-18: HR reviews details extracted from a text-layer CV
    {
        var extraction = CandidateTextParser.Parse(
            "Name: Ada ExampleEmail: ada@example.testPhone: +1 555 123 4567Skills: SQL, C#, SQL ServerExperience: Data analysis");

        extraction.FullName.ShouldBe("Ada Example");
        extraction.ContactEmail.ShouldBe("ada@example.test");
        extraction.ContactPhone.ShouldBe("+1 555 123 4567");
        extraction.Skills.ShouldBe(new[] { "SQL", "C#", "SQL Server" });
    }

    [Fact]
    public void Parse_ShouldLeaveNameEmptyWhenTextBeginsWithASection() // US-18: missing candidate details remain empty for review
    {
        var extraction = CandidateTextParser.Parse(
            "Skills: SQL, C#\nExperience: Data analysis");

        extraction.FullName.ShouldBeNull();
        extraction.Skills.ShouldBe(new[] { "SQL", "C#" });
    }

    [Fact]
    public void Calculate_ShouldUseExactTrimmedCaseInsensitiveRequirementMatching() // domain: Requirement Match is exact after trimming and case-insensitive comparison
    {
        var match = CandidateMatching.Calculate(
            ["SQL", "C#", "JavaScript"],
            [" sql ", "C# ", "SQL Server"]);

        match.MatchedRequirements.ShouldBe(2);
        match.TotalRequirements.ShouldBe(3);
    }
}
