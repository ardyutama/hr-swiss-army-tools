using hr_sat.Server.Domain.Vacancies;
using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Vacancies;

namespace hr_sat.Server.Features.Vacancies;

internal static class VacancyWrite
{
    public static async Task<Vacancy?> ExecuteAsync(
        long id,
        AppDbContext dbContext,
        Action<Vacancy> mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(id, cancellationToken);
        if (vacancy is null)
        {
            return null;
        }

        mutation(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return vacancy;
    }
}