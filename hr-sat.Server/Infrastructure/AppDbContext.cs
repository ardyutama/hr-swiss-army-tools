using hr_swiss_army_tools.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace hr_swiss_army_tools.Server.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vacancy>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Title).HasMaxLength(200).IsRequired();
        });
    }
}
