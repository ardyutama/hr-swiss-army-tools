using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Infrastructure;

public static class AppDbContextSeeder
{
    public static async Task SeedAsync(
        this AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Vacancies.AnyAsync(cancellationToken))
        {
            return;
        }

        var vacancies = new List<Vacancy>
        {
            Vacancy.Create(
                "Senior Backend Engineer",
                new DateOnly(2026, 8, 1),
                ["C#", "ASP.NET Core", "PostgreSQL"]).Value,
            Vacancy.Create(
                "People Operations Specialist",
                new DateOnly(2026, 8, 5),
                ["Recruitment", "Employee Relations", "HRIS"]).Value
        };

        var closedVacancy = Vacancy.Create(
            "Product Designer",
            new DateOnly(2026, 7, 1),
            ["Figma", "User Research", "Design Systems"]).Value;
        closedVacancy.Close(new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero));
        vacancies.Add(closedVacancy);

        dbContext.Vacancies.AddRange(vacancies);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}