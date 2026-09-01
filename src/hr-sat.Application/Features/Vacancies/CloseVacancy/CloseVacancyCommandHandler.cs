using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class CloseVacancyCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider)
    : ICommandHandler<CloseVacancyCommand, VacancyDetailsResponse>
{
    public async Task<Result<VacancyDetailsResponse>> Handle(
        CloseVacancyCommand command,
        CancellationToken cancellationToken)
    {
        var closeResult = await VacancyWrite.ExecuteAsync(
            command.Id,
            dbContext,
            vacancy => vacancy.Close(timeProvider.GetUtcNow()),
            cancellationToken);

        if (closeResult.IsFailure)
        {
            return Result<VacancyDetailsResponse>.Failure(closeResult.Error);
        }

        return await VacancyProgress.GetDetailsAsync(closeResult.Value, dbContext, cancellationToken);
    }
}
