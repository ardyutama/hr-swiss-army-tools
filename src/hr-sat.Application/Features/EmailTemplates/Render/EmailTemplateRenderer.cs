using System.Text.RegularExpressions;

namespace hr_sat.Application.Features.EmailTemplates.Render;

internal static partial class EmailTemplateRenderer
{
    [GeneratedRegex(
        @"\{\{\s*(candidate_name|vacancy_title)\s*\}\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public static RenderedEmailTemplateResponse Render(
        string subject,
        string body,
        string candidateName,
        string vacancyTitle)
    {
        return new RenderedEmailTemplateResponse(
            Replace(subject, candidateName, vacancyTitle),
            Replace(body, candidateName, vacancyTitle));
    }

    private static string Replace(
        string value,
        string candidateName,
        string vacancyTitle) => PlaceholderRegex().Replace(
            value,
            match => string.Equals(
                match.Groups[1].Value,
                "candidate_name",
                StringComparison.OrdinalIgnoreCase)
                ? candidateName
                : vacancyTitle);
}