using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Candidates.UpdateNotes;

internal sealed class UpdateCandidateNotesCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateNotesCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateNotesCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await CandidateWrite.ExecuteAsync(
            command.VacancyId,
            command.CandidateId,
            dbContext,
            (_, candidate) => candidate.UpdateNotes(command.Notes),
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
