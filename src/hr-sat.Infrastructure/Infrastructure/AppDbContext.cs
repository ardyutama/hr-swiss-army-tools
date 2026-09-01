using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace hr_sat.Infrastructure;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDomainEventDispatcher? domainEventDispatcher = null)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<VacancyRequirement> VacancyRequirements => Set<VacancyRequirement>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
    public DbSet<PendingFileDeletion> PendingFileDeletions => Set<PendingFileDeletion>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public async Task<Vacancy?> FindVacancyForUpdateAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A transaction is required before locking a vacancy.");
        }

        var vacancy = await Vacancies
            .FromSqlInterpolated($"SELECT * FROM vacancy WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (vacancy is not null)
        {
            await Entry(vacancy)
                .Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return vacancy;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var trackedEntities = ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();
        var domainEvents = trackedEntities
            .SelectMany(entity => entity.DomainEvents)
            .ToArray();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in trackedEntities)
        {
            entity.ClearDomainEvents();
        }

        if (domainEventDispatcher is not null && domainEvents.Length > 0)
        {
            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.Entity<Vacancy>().Ignore(item => item.DomainEvents);
        modelBuilder.Entity<VacancyRequirement>().Ignore(item => item.DomainEvents);
        modelBuilder.Entity<Candidate>().Ignore(item => item.DomainEvents);
        modelBuilder.Entity<CvDocument>().Ignore(item => item.DomainEvents);
    }
}
