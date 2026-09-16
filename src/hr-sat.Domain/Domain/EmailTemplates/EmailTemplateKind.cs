namespace hr_sat.Domain.EmailTemplates;

public enum EmailTemplateKind
{
    Shortlisted,
    Rejected
}

public static class EmailTemplateKindExtensions
{
    public static bool TryParse(string? value, out EmailTemplateKind kind)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "shortlisted":
                kind = EmailTemplateKind.Shortlisted;
                return true;
            case "rejected":
                kind = EmailTemplateKind.Rejected;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string ToApiValue(this EmailTemplateKind kind) => kind switch
    {
        EmailTemplateKind.Shortlisted => "shortlisted",
        EmailTemplateKind.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown email template kind.")
    };
}