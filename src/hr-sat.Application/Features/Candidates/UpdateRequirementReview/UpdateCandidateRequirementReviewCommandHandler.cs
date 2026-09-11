using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.UpdateRequirementReview;

internal sealed class UpdateCandidateRequirementReviewCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateRequirementReviewCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateRequirementReviewCommand command,
        CancellationToken cancellationToken)
    {
        var requirementExists = await dbContext.VacancyRequirements.AnyAsync(
            requirement => requirement.Id == command.RequirementId &&
                requirement.VacancyId == command.VacancyId,
            cancellationToken);
        if (!requirementExists)
        {
            return Result<CandidateDetailsResponse>.Failure(CandidateErrors.NotFound(command.RequirementId));
        }

        var updateResult = await RoundWrite.ExecuteCandidateAsync(
            command.VacancyId,
            command.RoundId,
            command.CandidateId,
            dbContext,
            (vacancy, candidateId) => vacancy.ReviewCandidateRequirement(
                command.RoundId,
                candidateId,
                command.RequirementId,
                command.Confirmed),
            cancellationToken);
        if (updateResult.IsFailure)
        {
            return Result<CandidateDetailsResponse>.Failure(updateResult.Error);
        }

        return await CandidateDetailsReader.ReadAsync(
            command.VacancyId,
            command.RoundId,
            command.CandidateId,
            dbContext,
            cancellationToken);
    }
}
