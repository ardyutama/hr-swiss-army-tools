using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.EmailTemplates.Render;

internal sealed class RenderEmailTemplateQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<RenderEmailTemplateQuery, RenderedEmailTemplateResponse>
{
    public async Task<Result<RenderedEmailTemplateResponse>> Handle(
        RenderEmailTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(query);
        if (validationError is not null)
        {
            return Result<RenderedEmailTemplateResponse>.Failure(validationError);
        }

        var candidateData = await (
            from candidateRecord in dbContext.Candidates.AsNoTracking()
            join round in dbContext.IntakeRounds.AsNoTracking()
                on candidateRecord.IntakeRoundId equals round.Id
            join vacancy in dbContext.Vacancies.AsNoTracking()
                on round.VacancyId equals vacancy.Id
            where candidateRecord.Id == query.CandidateId && vacancy.Id == query.VacancyId
            select new
            {
                candidateRecord.FullName,
                candidateRecord.SourceSenderName,
                VacancyTitle = vacancy.Title
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidateData is null)
        {
            return Result<RenderedEmailTemplateResponse>.Failure(
                CandidateErrors.NotFound(query.CandidateId));
        }

        var candidateName = CandidateDisplayName.Resolve(
            candidateData.FullName,
            candidateData.SourceSenderName);
        var rendered = EmailTemplateRenderer.Render(
            query.Subject!,
            query.Body!,
            candidateName,
            candidateData.VacancyTitle);
        return new RenderedEmailTemplateResponse(rendered.Subject, rendered.Body);
    }

    private static ValidationError? Validate(RenderEmailTemplateQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.VacancyId <= 0)
        {
            errors["vacancyId"] = ["VacancyId must be greater than 0."];
        }

        if (query.CandidateId <= 0)
        {
            errors["candidateId"] = ["CandidateId must be greater than 0."];
        }

        if (string.IsNullOrWhiteSpace(query.Subject) || query.Subject.Trim().Length > 998)
        {
            errors["subject"] = ["Subject must contain between 1 and 998 characters after trimming."];
        }

        if (string.IsNullOrWhiteSpace(query.Body))
        {
            errors["body"] = ["Body must not be blank."];
        }

        return errors.Count == 0 ? null : EmailTemplateErrors.Invalid(errors);
    }
}