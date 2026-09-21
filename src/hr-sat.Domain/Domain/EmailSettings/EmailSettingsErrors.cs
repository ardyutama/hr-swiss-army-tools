namespace hr_sat.Domain.EmailSettings;

using hr_sat.Domain;

public static class EmailSettingsErrors
{
    // The connection probe refused the values. The message arrives stage-tagged and
    // sanitized from the Infrastructure tester; raw exception text never crosses the
    // wire (issue 02, decision 12).
    public static Error TestFailed(string message) => Error.Problem(
        "EmailSettings.TestFailed",
        message);

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("EmailSettings.Invalid", "The SMTP settings are invalid.", errors);

    // Microsoft sign-in isn't configured on this installation (no Smtp:MicrosoftClientId
    // key); the client disables the choice from the GET's microsoftSignInAvailable, so
    // this guards a hand-rolled begin (issue 03, decision 24).
    public static Error MicrosoftSignInUnavailable() => Error.Problem(
        "EmailSettings.MicrosoftSignInUnavailable",
        "Microsoft sign-in isn't set up on this installation — whoever installed the app can add the Microsoft client ID to the configuration file and restart the app.");

    // A Microsoft Account save without a connected grant (issue 03, decision 30): the
    // settings page's connect flow is the only way to produce one (connect is the
    // test, decision 6).
    public static Error MicrosoftSignInRequired() => Error.Problem(
        "EmailSettings.MicrosoftSignInRequired",
        "Connect a Microsoft account on this page before saving.");
}
