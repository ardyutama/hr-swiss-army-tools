using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.UpdateDetails;

internal sealed class UpdateCandidateDetailsCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateDetailsCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await RoundWrite.ExecuteAsync(
            command.VacancyId,
            command.RoundId,
            dbContext,
            (vacancy, targetRoundId) => vacancy.EnsureCanReviewCandidate(targetRoundId),
            async (_, round) =>
            {
                var candidate = await dbContext.Candidates
                    .SingleOrDefaultAsync(
                        item => item.Id == command.CandidateId && item.IntakeRoundId == round.Id,
                        cancellationToken);
                if (candidate is null)
                {
                    return Result<Candidate>.Failure(CandidateErrors.NotFound(command.CandidateId));
                }

                var mutationResult = candidate.UpdateDetails(command.FullName, command.ContactEmail);
                return mutationResult.IsFailure
                    ? Result<Candidate>.Failure(mutationResult.Error)
                    : Result<Candidate>.Success(candidate);
            },
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
