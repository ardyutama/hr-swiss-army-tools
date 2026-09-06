using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.IntakeRounds.Create;

internal sealed class CreateIntakeRoundCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<CreateIntakeRoundCommand, IntakeRoundResponse>
{
    public async Task<Result<IntakeRoundResponse>> Handle(
        CreateIntakeRoundCommand command,
        CancellationToken cancellationToken)
    {
        var createResult = await VacancyWrite.ExecuteAsync(
            command.VacancyId,
            dbContext,
            vacancy => vacancy.CreateRound(command.Name),
            cancellationToken);
        if (createResult.IsFailure)
        {
            return Result<IntakeRoundResponse>.Failure(createResult.Error);
        }

        var vacancy = createResult.Value;
        var round = vacancy.Rounds.Single(item => item.IsOpen);
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