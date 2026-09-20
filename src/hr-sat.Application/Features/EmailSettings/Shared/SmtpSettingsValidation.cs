namespace hr_sat.Application.Features.EmailSettings.Shared;

// Request-shape rules shared by the test and upsert validators (issue 02, decision 6).
// The client mirrors them for feedback speed; the server stays the authority.
internal static class SmtpSettingsValidation
{
    public const int FromNameMaxLength = 200;

    // Bare hostname or IP address — no scheme, no slashes (Uri.CheckHostName rejects
    // both).
    public static bool IsValidHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) &&
        Uri.CheckHostName(host.Trim()) != UriHostNameType.Unknown;

    public static bool IsValidPort(int? port) => port is >= 1 and <= 65535;

    // Strict addr-spec: a display name or angle brackets make the input differ from
    // the parsed address, which is rejected.
    public static bool IsValidEmailAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var address = new System.Net.Mail.MailAddress(value.Trim());
            return address.Address == value.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
