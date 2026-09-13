using hr_sat.Domain;

namespace hr_sat.Domain.EmailTemplates;

public sealed class EmailTemplate : Entity
{
    private EmailTemplate()
    {
    }

    private EmailTemplate(
        long vacancyId,
        EmailTemplateKind kind,
        string subject,
        string body)
    {
        VacancyId = vacancyId;
        Kind = kind;
        Subject = subject;
        Body = body;
    }

    public long VacancyId { get; private set; }
    public EmailTemplateKind Kind { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    internal static Result<EmailTemplate> Create(
        long vacancyId,
        EmailTemplateKind kind,
        string? subject,
        string? body)
    {
        var contentResult = EmailTemplateRules.Validate(kind, subject, body);
        if (contentResult.IsFailure)
        {
            return Result<EmailTemplate>.Failure(contentResult.Error);
        }

        var content = contentResult.Value;
        return new EmailTemplate(vacancyId, kind, content.Subject, content.Body);
    }

    internal Result Replace(string? subject, string? body)
    {
        var contentResult = EmailTemplateRules.Validate(Kind, subject, body);
        if (contentResult.IsFailure)
        {
            return contentResult;
        }

        Subject = contentResult.Value.Subject;
        Body = contentResult.Value.Body;
        return Result.Success();
    }
}