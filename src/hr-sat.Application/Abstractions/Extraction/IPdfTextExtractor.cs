namespace hr_sat.Application.Abstractions.Extraction;

public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken);
}
