using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Application.Features.EmailTemplates.Delete;

internal sealed class DeleteEmailTemplateCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<DeleteEmailTemplateCommand>
{
    public async Task<Result> Handle(
        DeleteEmailTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (!EmailTemplateKindExtensions.TryParse(command.Kind, out var kind))
        {
            return InvalidKind();
        }

        return await VacancyWrite.ExecuteAsync(
            command.VacancyId,
            dbContext,
            vacancy => vacancy.DeleteEmailTemplate(kind),
            cancellationToken);
    }

    private static ValidationError InvalidKind() => EmailTemplateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["kind"] = ["Kind must be shortlisted or rejected."]
        });
}