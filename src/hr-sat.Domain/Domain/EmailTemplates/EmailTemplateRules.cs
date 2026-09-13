namespace hr_sat.Domain.EmailTemplates;

using hr_sat.Domain;

internal static class EmailTemplateRules
{
    public static Result EnsureSupportedKind(EmailTemplateKind kind)
    {
        if (Enum.IsDefined(kind))
        {
            return Result.Success();
        }

        return EmailTemplateErrors.Invalid(new Dictionary<string, string[]>
        {
            ["kind"] = ["Kind must be shortlisted or rejected."]
        });
    }

    public static Result<(string Subject, string Body)> Validate(
        EmailTemplateKind kind,
        string? subject,
        string? body)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.IsDefined(kind))
        {
            errors["kind"] = ["Kind must be shortlisted or rejected."];
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 998)
        {
            errors["subject"] = ["Subject must contain between 1 and 998 characters after trimming."];
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            errors["body"] = ["Body must not be blank."];
        }

        return errors.Count > 0
            ? Result<(string Subject, string Body)>.Failure(EmailTemplateErrors.Invalid(errors))
            : Result<(string Subject, string Body)>.Success((subject!.Trim(), body!));
    }
}