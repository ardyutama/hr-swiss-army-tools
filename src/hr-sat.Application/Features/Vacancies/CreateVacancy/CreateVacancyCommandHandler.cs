using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Vacancies;

internal sealed class CreateVacancyCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<CreateVacancyCommand, VacancyDetailsResponse>
{
    public async Task<Result<VacancyDetailsResponse>> Handle(
        CreateVacancyCommand command,
        CancellationToken cancellationToken)
    {
        var createResult = Vacancy.Create(command.Title, command.OpenedOn, command.Requirements);
        if (createResult.IsFailure)
        {
            return Result<VacancyDetailsResponse>.Failure(createResult.Error);
        }

        var vacancy = createResult.Value;

        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        return VacancyDetailsResponse.From(vacancy);
    }
}
