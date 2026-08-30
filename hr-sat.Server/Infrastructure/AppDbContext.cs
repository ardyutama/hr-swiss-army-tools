using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Server.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<VacancyRequirement> VacancyRequirements => Set<VacancyRequirement>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
