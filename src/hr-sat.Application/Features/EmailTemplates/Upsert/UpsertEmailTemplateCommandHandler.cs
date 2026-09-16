using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Application.Features.EmailTemplates.Upsert;

internal sealed class UpsertEmailTemplateCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpsertEmailTemplateCommand, EmailTemplateResponse>
{
    public async Task<Result<EmailTemplateResponse>> Handle(
        UpsertEmailTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (!EmailTemplateKindExtensions.TryParse(command.Kind, out var kind))
        {
            return Result<EmailTemplateResponse>.Failure(InvalidKind());
        }

        var upsertResult = await VacancyWrite.ExecuteAsync(
            command.VacancyId,
            dbContext,
            vacancy => vacancy.UpsertEmailTemplate(kind, command.Subject, command.Body),
            cancellationToken);
        if (upsertResult.IsFailure)
        {
            return Result<EmailTemplateResponse>.Failure(upsertResult.Error);
        }

        return EmailTemplateResponse.From(
            upsertResult.Value.EmailTemplates.Single(template => template.Kind == kind));
    }

    private static ValidationError InvalidKind() => EmailTemplateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["kind"] = ["Kind must be shortlisted or rejected."]
        });
}