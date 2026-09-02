using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Infrastructure.Extraction;
using Shouldly;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class PdfTextExtractionTests
{
    [Fact]
    public async Task ExtractTextAsync_ShouldPreserveVisualLinesSoSkillHeadingsStayParseable() // US-14/US-18: extracted skills feed requirement matching
    {
        var pdfBytes = BuildPdf("Skills", "C#, PostgreSQL");
        await using var stream = new MemoryStream(pdfBytes);

        var text = await new PdfPigTextExtractor().ExtractTextAsync(stream, CancellationToken.None);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.ShouldContain("Skills");
        lines.ShouldContain("C#, PostgreSQL");
    }

    [Fact]
    public async Task ExtractedText_ShouldFeedParserAndMatchingFromRealLayout() // regression: flattened page text yielded zero extracted skills
    {
        var pdfBytes = BuildPdf("Skills", "C#, PostgreSQL");
        await using var stream = new MemoryStream(pdfBytes);

        var text = await new PdfPigTextExtractor().ExtractTextAsync(stream, CancellationToken.None);
        var extraction = CandidateTextParser.Parse(text);
        var match = CandidateMatching.Calculate(["C#", "ASP.NET Core", "PostgreSQL"], extraction.Skills);

        extraction.Skills.ShouldBe(["C#", "PostgreSQL"]);
        match.MatchedRequirements.ShouldBe(2);
        match.TotalRequirements.ShouldBe(3);
    }

    private static byte[] BuildPdf(params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var y = 800;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(50, y), font);
            y -= 20;
        }

        return builder.Build();
    }
}
