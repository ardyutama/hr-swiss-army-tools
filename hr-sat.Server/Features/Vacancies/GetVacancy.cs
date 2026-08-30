using hr_sat.Server.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Features.Vacancies;

internal static class GetVacancy
{
    public static async Task<Results<Ok<VacancyDetailsResponse>, NotFound>> HandleAsync(
        long id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Include(item => item.Requirements)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return vacancy is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(VacancyDetailsResponse.From(vacancy));
    }
}