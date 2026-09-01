using System.Text.RegularExpressions;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.Shared;

internal static partial class CandidateTextParser
{
    private static readonly string[] SkillHeadings =
    [
        "skill",
        "skills",
        "technical skill",
        "technical skills",
        "core competency",
        "core competencies",
        "competency",
        "competencies",
        "technology",
        "technologies",
        "proficiency",
        "proficiencies"
    ];

    private static readonly string[] SectionHeadings =
    [
        "summary",
        "profile",
        "experience",
        "professional experience",
        "work experience",
        "employment",
        "education",
        "certification",
        "certifications",
        "project",
        "projects",
        "languages",
        "references"
    ];

    public static CandidateExtraction Parse(string text)
    {
        var normalizedText = LabeledLineRegex().Replace(text, "\n$0");
        normalizedText = ConcatenatedLabelRegex().Replace(normalizedText, "\n$0");
        var lines = normalizedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return new CandidateExtraction(
            FindName(lines),
            FindEmail(lines),
            FindPhone(lines),
            FindSkills(lines));
    }

    private static string? FindName(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = NameLineRegex().Match(line);
            if (match.Success)
            {
                return match.Groups["value"].Value.Trim();
            }
        }

        foreach (var line in lines)
        {
            var heading = NormalizeHeading(line);
            if (IsSkillHeading(heading) || IsSectionHeading(heading) || HasBulletPrefix(line))
            {
                break;
            }

            if (EmailRegex().IsMatch(line) || IsPhoneLine(line) || IsMetadataLine(line))
            {
                continue;
            }

            if (line.Length <= 300 && line.Any(char.IsLetter))
            {
                return line;
            }
        }

        return null;
    }

    private static string? FindEmail(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = EmailRegex().Match(line);
            if (match.Success)
            {
                return match.Value.Trim().TrimEnd('.', ',', ';');
            }
        }

        return null;
    }

    private static string? FindPhone(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = PhoneRegex().Match(line);
            if (!match.Success || match.Value.Count(char.IsDigit) < 7)
            {
                continue;
            }

            if (!IsPhoneLine(line) && DateLineRegex().IsMatch(match.Value))
            {
                continue;
            }

            return match.Value.Trim();
        }

        return null;
    }

    private static IReadOnlyList<string> FindSkills(IReadOnlyList<string> lines)
    {
        var skills = new List<string>();
        var inSkillsSection = false;

        foreach (var line in lines)
        {
            var heading = NormalizeHeading(line);
            if (IsSkillHeading(heading))
            {
                inSkillsSection = true;
                AddSkills(skills, GetHeadingValue(line));
                continue;
            }

            if (inSkillsSection && IsSectionHeading(heading))
            {
                inSkillsSection = false;
                continue;
            }

            if (inSkillsSection || HasBulletPrefix(line))
            {
                AddSkills(skills, line);
            }
        }

        return skills
            .Select(skill => skill.Trim())
            .Where(skill => skill.Length is > 0 and <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddSkills(ICollection<string> skills, string line)
    {
        var value = RemoveBulletPrefix(line).Trim();
        if (value.Length == 0)
        {
            return;
        }

        foreach (var skill in value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = skill.Trim();
            if (normalized.Length > 0)
            {
                skills.Add(normalized);
            }
        }
    }

    private static bool IsSkillHeading(string heading) =>
        SkillHeadings.Contains(heading, StringComparer.OrdinalIgnoreCase);

    private static bool IsSectionHeading(string heading) =>
        SectionHeadings.Contains(heading, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeHeading(string line)
    {
        var separator = line.IndexOf(':');
        var heading = separator >= 0 ? line[..separator] : line;
        return heading.Trim().TrimEnd(':').Trim().ToLowerInvariant();
    }

    private static string GetHeadingValue(string line)
    {
        var separator = line.IndexOf(':');
        return separator < 0 ? string.Empty : line[(separator + 1)..];
    }

    private static bool HasBulletPrefix(string line) =>
        line.Length > 0 && "•●▪◦-*".Contains(line[0]);

    private static string RemoveBulletPrefix(string line) =>
        HasBulletPrefix(line) ? line[1..] : line;

    private static bool IsPhoneLine(string line) => PhoneLabelRegex().IsMatch(line);

    private static bool IsMetadataLine(string line) =>
        NameLineRegex().IsMatch(line) ||
        PhoneLabelRegex().IsMatch(line) ||
        line.StartsWith("date:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("address:", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^(?:full\\s+name|candidate\\s+name|name)\\s*[:\\-]\\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NameLineRegex();

    [GeneratedRegex("[A-Z0-9._%+\\-]+@[A-Z0-9.\\-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex("(?<!\\d)\\+?\\d[\\d\\s().\\-]{5,}\\d(?!\\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex("^(?:phone|mobile|telephone|tel|contact\\s+number)\\s*[:\\-]", RegexOptions.IgnoreCase)]
    private static partial Regex PhoneLabelRegex();

    [GeneratedRegex("^\\d{4}[-/.]\\d{1,2}[-/.]\\d{1,2}$")]
    private static partial Regex DateLineRegex();

    [GeneratedRegex("(?<![A-Za-z])(?:full\\s+name|candidate\\s+name|name|email|phone|mobile|telephone|tel|contact\\s+number|skills?|technical\\s+skills?|core\\s+competenc(?:y|ies)|competenc(?:y|ies)|technolog(?:y|ies)|proficienc(?:y|ies)|summary|profile|professional\\s+experience|work\\s+experience|experience|employment|education|certifications?|projects?|languages|references)\\s*[:\\-]", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledLineRegex();

    [GeneratedRegex("(?<=[a-z0-9)])(?:Full\\s+Name|Candidate\\s+Name|Name|Email|Phone|Mobile|Telephone|Tel|Contact\\s+Number|Skill|Skills|Technical\\s+Skill|Technical\\s+Skills|Core\\s+Competency|Core\\s+Competencies|Competency|Competencies|Technology|Technologies|Proficiency|Proficiencies|Summary|Profile|Professional\\s+Experience|Work\\s+Experience|Experience|Employment|Education|Certification|Certifications|Project|Projects|Languages|References)\\s*[:\\-]")]
    private static partial Regex ConcatenatedLabelRegex();
}
