using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class ReopenVacancyCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<ReopenVacancyCommand, VacancyDetailsResponse>
{
    public async Task<Result<VacancyDetailsResponse>> Handle(
        ReopenVacancyCommand command,
        CancellationToken cancellationToken)
    {
        var reopenResult = await VacancyWrite.ExecuteAsync(
            command.Id,
            dbContext,
            vacancy => vacancy.Reopen(),
            cancellationToken);

        if (reopenResult.IsFailure)
        {
            return Result<VacancyDetailsResponse>.Failure(reopenResult.Error);
        }

        return await VacancyProgress.GetDetailsAsync(reopenResult.Value, dbContext, cancellationToken);
    }
}
