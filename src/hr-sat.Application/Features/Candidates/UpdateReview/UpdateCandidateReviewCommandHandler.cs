using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateReview;

internal sealed class UpdateCandidateReviewCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateReviewCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateReviewCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CandidateReviewStatus>(command.ReviewStatus, true, out var reviewStatus))
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.Invalid(new Dictionary<string, string[]>
                {
                    ["reviewStatus"] = ["Review status must be shortlisted, flagged, or rejected."]
                }));
        }

        var updateResult = await CandidateWrite.ExecuteAsync(
            command.VacancyId,
            command.CandidateId,
            dbContext,
            (_, candidate) => candidate.ApplyReview(reviewStatus, command.Notes),
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
