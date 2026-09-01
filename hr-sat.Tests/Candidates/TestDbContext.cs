using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace hr_sat.Tests.Candidates;

internal sealed class TestDbContext : DbContext, IApplicationDbContext
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public TestDbContext()
    {
        connection.Open();
        Database.EnsureCreated();
    }

    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<VacancyRequirement> VacancyRequirements => Set<VacancyRequirement>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public Task<Vacancy?> FindVacancyForUpdateAsync(
        long id,
        CancellationToken cancellationToken) =>
        Vacancies
            .Include(vacancy => vacancy.Requirements)
            .SingleOrDefaultAsync(vacancy => vacancy.Id == id, cancellationToken);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite(connection);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.UtcDateTime,
            value => new DateTimeOffset(value));
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
            value => value.HasValue ? value.Value.UtcDateTime : null,
            value => value.HasValue ? new DateTimeOffset(value.Value) : null);

        modelBuilder.Entity<Vacancy>(entity =>
        {
            entity.HasKey(vacancy => vacancy.Id);
            entity.Property(vacancy => vacancy.Id).ValueGeneratedOnAdd();
            entity.Property(vacancy => vacancy.ClosedAt)
                .HasConversion(nullableDateTimeOffsetConverter);
            entity.Property(vacancy => vacancy.CreatedAt)
                .HasConversion(dateTimeOffsetConverter);
            entity.HasMany(vacancy => vacancy.Requirements)
                .WithOne()
                .HasForeignKey(requirement => requirement.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(vacancy => vacancy.Candidates)
                .WithOne()
                .HasForeignKey(candidate => candidate.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(vacancy => vacancy.Requirements)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(vacancy => vacancy.Candidates)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<VacancyRequirement>(entity =>
        {
            entity.HasKey(requirement => requirement.Id);
            entity.Property(requirement => requirement.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Id).ValueGeneratedOnAdd();
            entity.Property(candidate => candidate.SourceSentAt)
                .HasConversion(nullableDateTimeOffsetConverter);
            entity.Property(candidate => candidate.ImportedAt)
                .HasConversion(dateTimeOffsetConverter);
            entity.Property(candidate => candidate.ReviewStatus).HasConversion<string>();
            entity.Property(candidate => candidate.ExtractionStatus).HasConversion<string>();
            entity.HasMany(candidate => candidate.CvDocuments)
                .WithOne()
                .HasForeignKey(document => document.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(candidate => candidate.CvDocuments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CvDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Id).ValueGeneratedOnAdd();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}