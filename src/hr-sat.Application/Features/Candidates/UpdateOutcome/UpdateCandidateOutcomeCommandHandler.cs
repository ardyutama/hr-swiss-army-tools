using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Candidates.UpdateOutcome;

internal sealed class UpdateCandidateOutcomeCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateOutcomeCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateOutcomeCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CandidateHireOutcome>(command.Outcome, true, out var outcome) ||
            !Enum.IsDefined(outcome))
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.Invalid(new Dictionary<string, string[]>
                {
                    ["outcome"] = ["Hire outcome must be none, hired, runaway, or declined."]
                }));
        }

        var updateResult = await RoundWrite.ExecuteCandidateAsync(
            command.VacancyId,
            command.RoundId,
            command.CandidateId,
            dbContext,
            (vacancy, candidateId) => vacancy.SetCandidateHireOutcome(
                command.RoundId,
                candidateId,
                outcome,
                command.Note),
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