using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class PurgeVacancy
{
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        long id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var deletedCount = await dbContext.Vacancies
            .Where(vacancy => vacancy.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }
}