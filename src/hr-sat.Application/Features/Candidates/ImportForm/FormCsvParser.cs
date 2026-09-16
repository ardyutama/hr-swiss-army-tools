using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.ImportForm;

internal static class FormCsvParser
{
    public static async Task<ParsedFormCsv> ParseAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.None,
            BadDataFound = context => throw new FormCsvParseException(
                $"CSV row {context.Context?.Parser?.Row ?? 1} contains malformed data."),
            MissingFieldFound = null,
            HeaderValidated = null,
            DetectColumnCountChanges = false
        });

        string[]? header;
        try
        {
            header = await parser.ReadAsync()
                ? parser.Record ?? Array.Empty<string>()
                : null;
        }
        catch (FormCsvParseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CsvHelperException or DecoderFallbackException)
        {
            throw new FormCsvParseException("CSV row 1 contains malformed data.", exception);
        }

        if (header is null || header.Length == 0)
        {
            throw new FormCsvParseException("The CSV must contain a header row.");
        }

        var rows = new List<ParsedFormRow>();
        var rowPosition = 1;
        try
        {
            while (await parser.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowPosition++;
                var cells = parser.Record ?? Array.Empty<string>();
                if (cells.Length != header.Length)
                {
                    throw new FormCsvParseException(
                        $"CSV row {rowPosition} has {cells.Length} cells; expected {header.Length}.");
                }

                var timestampRaw = cells[0];
                rows.Add(new ParsedFormRow(
                    rowPosition,
                    cells,
                    timestampRaw,
                    ParseTimestamp(timestampRaw),
                    CandidateFormIdentity.Detect(cells)));
            }
        }
        catch (FormCsvParseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CsvHelperException or DecoderFallbackException)
        {
            throw new FormCsvParseException(
                $"CSV row {rowPosition} contains malformed data.",
                exception);
        }

        if (rows.Count == 0)
        {
            throw new FormCsvParseException("The CSV must contain at least one data row.");
        }

        return new ParsedFormCsv(rows);
    }

    private static DateTimeOffset? ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces |
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}

internal sealed record ParsedFormCsv(IReadOnlyList<ParsedFormRow> Rows);

internal sealed record ParsedFormRow(
    int RowPosition,
    IReadOnlyList<string> Cells,
    string FormTimestampRaw,
    DateTimeOffset? FormTimestampParsed,
    string? IdentityKey);

internal sealed class FormCsvParseException : Exception
{
    public FormCsvParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}