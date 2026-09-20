using System.Text.RegularExpressions;

namespace hr_sat.Domain.EmailTemplates;

// Placeholder resolution for an Email Template: the candidate's name and the vacancy's
// title resolve per candidate when a message is generated (CONTEXT.md, Email Template).
// Used by the render query and by Dispatch sending alike.
public static partial class EmailTemplateRenderer
{
    [GeneratedRegex(
        @"\{\{\s*(candidate_name|vacancy_title)\s*\}\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public static RenderedEmailTemplate Render(
        string subject,
        string body,
        string candidateName,
        string vacancyTitle)
    {
        return new RenderedEmailTemplate(
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
