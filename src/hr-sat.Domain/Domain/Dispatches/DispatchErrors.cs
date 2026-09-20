using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Dispatches;

public static class DispatchErrors
{
    public static Error SmtpNotConfigured() => Error.Problem(
        "Dispatch.SmtpNotConfigured",
        "Email sending is not configured: the 'Smtp' section is missing or incomplete in the app's configuration file (appsettings.json). Whoever installed this app can fill it in from the included template and restart the app.");

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
