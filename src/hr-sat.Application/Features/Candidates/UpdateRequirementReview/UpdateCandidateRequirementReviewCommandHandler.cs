using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateRequirementReview;

internal sealed class UpdateCandidateRequirementReviewCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateRequirementReviewCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateRequirementReviewCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await CandidateWrite.ExecuteAsync(
            command.VacancyId,
            command.CandidateId,
            dbContext,
            (vacancy, candidate) =>
            {
                if (vacancy.Requirements.All(requirement => requirement.Id != command.RequirementId))
                {
                    return CandidateErrors.NotFound(command.RequirementId);
                }

                return candidate.SetRequirementReview(command.RequirementId, command.Confirmed);
            },
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
