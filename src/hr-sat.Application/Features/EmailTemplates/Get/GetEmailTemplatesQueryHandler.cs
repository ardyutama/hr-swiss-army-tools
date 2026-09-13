using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.EmailTemplates.Get;

internal sealed class GetEmailTemplatesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateResponse>>
{
    public async Task<Result<IReadOnlyList<EmailTemplateResponse>>> Handle(
        GetEmailTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == query.VacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return Result<IReadOnlyList<EmailTemplateResponse>>.Failure(
                VacancyErrors.NotFound(query.VacancyId));
        }

        var templates = await dbContext.EmailTemplates
            .AsNoTracking()
            .Where(template => template.VacancyId == query.VacancyId)
            .OrderBy(template => template.Kind == EmailTemplateKind.Shortlisted ? 0 : 1)
            .Select(template => new EmailTemplateResponse(
                template.Id,
                template.VacancyId,
                template.Kind.ToString().ToLowerInvariant(),
                template.Subject,
                template.Body))
            .ToListAsync(cancellationToken);

        return templates;
    }
}