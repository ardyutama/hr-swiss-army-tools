using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.GetDetails;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Candidates.UpdateNotes;

internal sealed class UpdateCandidateNotesCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateCandidateNotesCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        UpdateCandidateNotesCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await RoundWrite.ExecuteCandidateAsync(
            command.VacancyId,
            command.RoundId,
            command.CandidateId,
            dbContext,
            (vacancy, candidateId) => vacancy.UpdateCandidateNotes(
                command.RoundId,
                candidateId,
                command.Notes),
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
