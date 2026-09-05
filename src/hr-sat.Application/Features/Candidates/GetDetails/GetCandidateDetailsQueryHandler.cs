using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.GetDetails;

internal sealed class GetCandidateDetailsQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetCandidateDetailsQuery, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        GetCandidateDetailsQuery query,
        CancellationToken cancellationToken)
    {
        return await CandidateDetailsReader.ReadAsync(
            query.VacancyId,
            query.CandidateId,
            dbContext,
            cancellationToken);
    }
}