using hr_sat.Domain;

namespace hr_sat.Domain.EmailTemplates;

public enum EmailTemplateKind
{
    Shortlisted,
    Rejected
}

public static class EmailTemplateKindExtensions
{
    public static bool TryParse(string? value, out EmailTemplateKind kind) =>
        EnumParsing.TryParseDefined<EmailTemplateKind>(value, out kind);

    public static string ToApiValue(this EmailTemplateKind kind) => kind switch
    {
        EmailTemplateKind.Shortlisted => "shortlisted",
        EmailTemplateKind.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown email template kind.")
    };
}