using hr_sat.Server.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Infrastructure.Vacancies;

internal static class VacancyWriteQueries
{
    public static async Task<Vacancy?> FindVacancyForUpdateAsync(
        this AppDbContext dbContext,
        long id,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A transaction is required before locking a vacancy.");
        }

        var vacancy = await dbContext.Vacancies
            .FromSqlInterpolated($"SELECT * FROM vacancy WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (vacancy is not null)
        {
            await dbContext.Entry(vacancy)
                .Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return vacancy;
    }
}