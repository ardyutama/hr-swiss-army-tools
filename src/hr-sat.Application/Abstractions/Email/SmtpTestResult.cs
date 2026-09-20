namespace hr_sat.Application.Abstractions.Email;

// The probe outcome. FailureMessage is stage-tagged and sanitized (issue 02, decision
// 12): "Could not connect to {host}:{port}." or "Authentication failed — check the app
// password." — never raw exception text.
public sealed record SmtpTestResult(bool Succeeded, string? FailureMessage)
{
    public static readonly SmtpTestResult Success = new(true, null);

    public static SmtpTestResult Failure(string message) => new(false, message);
}
