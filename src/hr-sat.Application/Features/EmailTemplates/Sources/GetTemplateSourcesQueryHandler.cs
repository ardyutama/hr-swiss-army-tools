using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.EmailTemplates.Sources;

internal sealed class GetTemplateSourcesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetTemplateSourcesQuery, IReadOnlyList<EmailTemplateSourceResponse>>
{
    public async Task<Result<IReadOnlyList<EmailTemplateSourceResponse>>> Handle(
        GetTemplateSourcesQuery query,
        CancellationToken cancellationToken)
    {
        if (!EmailTemplateKindExtensions.TryParse(query.Kind, out var kind))
        {
            return Result<IReadOnlyList<EmailTemplateSourceResponse>>.Failure(InvalidKind());
        }

        var vacancyExists = await dbContext.Vacancies
            .AsNoTracking()
            .AnyAsync(vacancy => vacancy.Id == query.VacancyId, cancellationToken);
        if (!vacancyExists)
        {
            return Result<IReadOnlyList<EmailTemplateSourceResponse>>.Failure(
                VacancyErrors.NotFound(query.VacancyId));
        }

        var sources = await (
            from template in dbContext.EmailTemplates.AsNoTracking()
            join vacancy in dbContext.Vacancies.AsNoTracking()
                on template.VacancyId equals vacancy.Id
            where template.Kind == kind && template.VacancyId != query.VacancyId
            orderby vacancy.OpenedOn descending, vacancy.Id descending
            select new EmailTemplateSourceResponse(
                vacancy.Id,
                vacancy.Title,
                vacancy.OpenedOn,
                template.Kind.ToString().ToLowerInvariant(),
                template.Subject,
                template.Body))
            .ToListAsync(cancellationToken);

        return sources;
    }

    private static ValidationError InvalidKind() => EmailTemplateErrors.Invalid(
        new Dictionary<string, string[]>
        {
            ["kind"] = ["Kind must be shortlisted or rejected."]
        });
}