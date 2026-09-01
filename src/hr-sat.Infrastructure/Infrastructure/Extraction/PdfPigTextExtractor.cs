using hr_sat.Application.Abstractions.Extraction;
using UglyToad.PdfPig;

namespace hr_sat.Infrastructure.Extraction;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(content);
        var text = string.Join(
            Environment.NewLine,
            document.GetPages().Select(page => page.Text));
        return Task.FromResult(text);
    }
}
