using System.Text;
using hr_sat.Application.Abstractions.Extraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace hr_sat.Infrastructure.Extraction;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private const double SameLineTolerance = 2.5;

    public Task<string> ExtractTextAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(content);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            // page.Text flattens whole pages into a single line for some producers
            // (e.g. Microsoft Word), which blinds the line-oriented CV parser.
            // Rebuild visual lines from word positions instead.
            var lines = new List<List<Word>>();
            foreach (var word in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = lines.FirstOrDefault(candidate =>
                    Math.Abs(candidate[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= SameLineTolerance);
                if (line is null)
                {
                    lines.Add([word]);
                }
                else
                {
                    line.Add(word);
                }
            }

            foreach (var line in lines)
            {
                builder.AppendLine(string.Join(
                    ' ',
                    line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            }
        }

        return Task.FromResult(builder.ToString());
    }
}
