using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.Get;

internal sealed class GetCandidateQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetCandidateQuery, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        GetCandidateQuery query,
        CancellationToken cancellationToken)
    {
        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Include(item => item.Requirements)
            .SingleOrDefaultAsync(item => item.Id == query.VacancyId, cancellationToken);
        if (vacancy is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(query.VacancyId));
        }

        var candidate = await dbContext.Candidates
            .AsNoTracking()
            .Include(item => item.CvDocuments)
            .Include(item => item.Skills)
            .SingleOrDefaultAsync(
                item => item.Id == query.CandidateId && item.VacancyId == query.VacancyId,
                cancellationToken);
        if (candidate is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(query.CandidateId));
        }

        return CandidateDetailsResponse.From(
            query.VacancyId,
            candidate,
            vacancy.Requirements.Select(requirement => requirement.Phrase));
    }
}
