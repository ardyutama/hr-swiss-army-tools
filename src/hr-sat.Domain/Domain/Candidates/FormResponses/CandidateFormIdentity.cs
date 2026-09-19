using System.Text.RegularExpressions;

namespace hr_sat.Domain.Candidates.FormResponses;

public static partial class CandidateFormIdentity
{
    public static string? Detect(IReadOnlyList<string> cells)
    {
        foreach (var cell in cells.Skip(1))
        {
            var normalizedEmail = NormalizeEmail(cell);
            if (normalizedEmail is not null)
            {
                return normalizedEmail;
            }
        }

        foreach (var cell in cells.Skip(1))
        {
            var digits = new string(cell.Where(char.IsDigit).ToArray());
            if (digits.Length >= 8)
            {
                return digits;
            }
        }

        return null;
    }

    public static string? NormalizeEmail(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is not null && EmailPattern().IsMatch(normalized)
            ? normalized
            : null;
    }

    public static bool IsEmailKey(string? value) => NormalizeEmail(value) is not null;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}