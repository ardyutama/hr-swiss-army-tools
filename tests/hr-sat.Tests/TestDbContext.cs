using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
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
    public DbSet<IntakeRound> IntakeRounds => Set<IntakeRound>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateRequirementReview> CandidateRequirementReviews => Set<CandidateRequirementReview>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
    public DbSet<PendingFileDeletion> PendingFileDeletions => Set<PendingFileDeletion>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public Task<Vacancy?> LockVacancyAsync(
        long id,
        CancellationToken cancellationToken) =>
        Vacancies.SingleOrDefaultAsync(vacancy => vacancy.Id == id, cancellationToken);

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
            entity.HasMany(vacancy => vacancy.Rounds)
                .WithOne()
                .HasForeignKey(round => round.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(vacancy => vacancy.Requirements)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(vacancy => vacancy.Rounds)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<VacancyRequirement>(entity =>
        {
            entity.HasKey(requirement => requirement.Id);
            entity.Property(requirement => requirement.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<IntakeRound>(entity =>
        {
            entity.HasKey(round => round.Id);
            entity.Property(round => round.Id).ValueGeneratedOnAdd();
            entity.Property(round => round.ClosedAt)
                .HasConversion(nullableDateTimeOffsetConverter);
            entity.HasMany(round => round.Candidates)
                .WithOne()
                .HasForeignKey(candidate => candidate.IntakeRoundId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(round => round.Candidates)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Id).ValueGeneratedOnAdd();
            entity.Property(candidate => candidate.IntakeRoundId).IsRequired();
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
            entity.HasMany(candidate => candidate.RequirementReviews)
                .WithOne()
                .HasForeignKey(review => review.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(candidate => candidate.CvDocuments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(candidate => candidate.RequirementReviews)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CandidateRequirementReview>(entity =>
        {
            entity.HasKey(review => review.Id);
            entity.Property(review => review.Id).ValueGeneratedOnAdd();
            entity.HasOne<VacancyRequirement>()
                .WithMany()
                .HasForeignKey(review => review.VacancyRequirementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CvDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PendingFileDeletion>(entity =>
        {
            entity.HasKey(deletion => deletion.Id);
            entity.Property(deletion => deletion.Id).ValueGeneratedOnAdd();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}