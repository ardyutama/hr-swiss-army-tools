namespace hr_sat.Application.Abstractions.Email;

// The SMTP Account's Sign-in Method (CONTEXT.md; issue 03, decisions 6 and 16): an
// App Password typed on the settings page, or a Microsoft Account connected through
// Microsoft's sign-in flow. The account carries exactly one method at a time;
// switching discards the other credential. Wire values are the glossary's kebab-case
// terms — "auth method" and "basic | oauth2" stay wire protocol, out of domain
// language.
public enum SignInMethod
{
    AppPassword,
    MicrosoftAccount,
}

public static class SignInMethodExtensions
{
    // Kebab-case wire values never match member names, so EnumParsing.TryParseDefined
    // cannot serve this enum; defined values only, case-insensitive (CONTEXT.md:
    // Enum Parsing).
    public static bool TryParse(string? value, out SignInMethod method)
    {
        method = default;
        if (string.Equals(value?.Trim(), "app-password", StringComparison.OrdinalIgnoreCase))
        {
            method = SignInMethod.AppPassword;
            return true;
        }

        if (string.Equals(value?.Trim(), "microsoft-account", StringComparison.OrdinalIgnoreCase))
        {
            method = SignInMethod.MicrosoftAccount;
            return true;
        }

        return false;
    }

    public static string ToApiValue(this SignInMethod method) => method switch
    {
        SignInMethod.AppPassword => "app-password",
        SignInMethod.MicrosoftAccount => "microsoft-account",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown sign-in method."),
    };
}
