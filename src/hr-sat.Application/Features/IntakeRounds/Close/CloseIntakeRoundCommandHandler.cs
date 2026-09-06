using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.IntakeRounds.Close;

internal sealed class CloseIntakeRoundCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<CloseIntakeRoundCommand, IntakeRoundResponse>
{
    public async Task<Result<IntakeRoundResponse>> Handle(
        CloseIntakeRoundCommand command,
        CancellationToken cancellationToken)
    {
        var closeResult = await VacancyWrite.ExecuteAsync(
            command.VacancyId,
            dbContext,
            vacancy => vacancy.CloseRound(command.RoundId, timeProvider.GetUtcNow()),
            cancellationToken);
        if (closeResult.IsFailure)
        {
            return Result<IntakeRoundResponse>.Failure(closeResult.Error);
        }

        var vacancy = closeResult.Value;
        var round = vacancy.Rounds.Single(item => item.Id == command.RoundId);
        return new IntakeRoundResponse(
            round.Id,
            vacancy.Id,
            round.RoundNumber,
            round.Name,
            round.IsOpen ? "open" : "closed",
            round.ClosedAt,
            await dbContext.Candidates.CountAsync(
                candidate => candidate.IntakeRoundId == round.Id,
                cancellationToken));
    }
}