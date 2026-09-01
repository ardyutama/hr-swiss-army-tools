using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class UpdateVacancyCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpdateVacancyCommand, VacancyDetailsResponse>
{
    public async Task<Result<VacancyDetailsResponse>> Handle(
        UpdateVacancyCommand command,
        CancellationToken cancellationToken)
    {
        var updateResult = await VacancyWrite.ExecuteAsync(
            command.Id,
            dbContext,
            vacancy => vacancy.UpdateDefinition(
                command.Title,
                command.OpenedOn,
                command.Requirements),
            cancellationToken);

        if (updateResult.IsFailure)
        {
            return Result<VacancyDetailsResponse>.Failure(updateResult.Error);
        }

        return await VacancyProgress.GetDetailsAsync(updateResult.Value, dbContext, cancellationToken);
    }
}
