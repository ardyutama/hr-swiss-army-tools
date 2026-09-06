using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Domain;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateDetails;

internal sealed class UpdateCandidateDetailsCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateDetailsCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await CandidateWrite.ExecuteAsync(
            command.VacancyId,
            command.CandidateId,
            dbContext,
            (_, candidate) => candidate.UpdateDetails(command.FullName, command.ContactEmail),
            cancellationToken);
        if (updateResult.IsFailure)
        {
            return Result<CandidateDetailsResponse>.Failure(updateResult.Error);
        }

        return await CandidateDetailsReader.ReadAsync(
            command.VacancyId,
            command.CandidateId,
            dbContext,
            cancellationToken);
    }
}
