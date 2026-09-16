namespace hr_sat.Domain.EmailTemplates;

using hr_sat.Domain;

public static class EmailTemplateErrors
{
    public static Error NotFound(long vacancyId, EmailTemplateKind kind) => Error.NotFound(
        "EmailTemplates.NotFound",
        $"Email template of kind '{kind.ToApiValue()}' for vacancy with id '{vacancyId}' was not found.");

    public static Error VacancyClosed(long vacancyId) => Error.Conflict(
        "EmailTemplates.VacancyClosed",
        $"Vacancy with id '{vacancyId}' is closed and must be reopened before email templates can be changed.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("EmailTemplates.Invalid", "The email template is invalid.", errors);
}