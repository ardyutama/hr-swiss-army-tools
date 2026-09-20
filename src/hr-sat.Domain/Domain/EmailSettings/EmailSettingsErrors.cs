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
}
