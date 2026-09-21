using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Dispatches;

public static class DispatchErrors
{
    public static Error SmtpNotConfigured() => Error.Problem(
        "Dispatch.SmtpNotConfigured",
        "Email sending is not configured: the 'Smtp' section is missing or incomplete in the app's configuration file (appsettings.json). Whoever installed this app can fill it in from the included template and restart the app.");

    // The dispatch pre-flight refusal for a dead Microsoft Account grant (issue 03,
    // decisions 7, 12): one amber refusal for the whole run, no dispatch rows written
    // — never a wall of identical per-dispatch failures.
    public static Error SmtpSignInExpired() => Error.Problem(
        "Dispatch.SmtpSignInExpired",
        "Reconnect the email account on the settings page.");

    public static Error MissingTemplate(EmailTemplateKind kind) => Error.Problem(
        "Dispatch.MissingTemplate",
        $"The vacancy has no '{kind.ToApiValue()}' email template, so nothing was sent.",
        new Dictionary<string, object?>
        {
            ["kind"] = kind.ToApiValue()
        });

    public static Error VacancyClosed(long vacancyId) => Error.Conflict(
        "Dispatch.VacancyClosed",
        $"Vacancy with id '{vacancyId}' is closed and can no longer send dispatches.");

    public static Error RunInProgress(long vacancyId, long roundId) => Error.Problem(
        "Dispatch.RunInProgress",
        $"A dispatch run is already in progress for round '{roundId}' of vacancy '{vacancyId}'.");

    public static Error RunNotFound(long runId) => Error.NotFound(
        "Dispatch.RunNotFound",
        $"Dispatch run with id '{runId}' was not found.");
}
