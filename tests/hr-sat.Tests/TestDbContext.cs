using System.Text.Json;
using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
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
    public DbSet<CandidateFormResponse> CandidateFormResponses => Set<CandidateFormResponse>();
    public DbSet<CandidateRequirementReview> CandidateRequirementReviews => Set<CandidateRequirementReview>();
    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
    public DbSet<PendingFileDeletion> PendingFileDeletions => Set<PendingFileDeletion>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<FormLayout> FormLayouts => Set<FormLayout>();
    public DbSet<ScreeningRuleSet> ScreeningRuleSets => Set<ScreeningRuleSet>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public async Task<Vacancy?> FindVacancyForUpdateAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A transaction is required before reading a vacancy for update.");
        }

        return await Vacancies.SingleOrDefaultAsync(vacancy => vacancy.Id == id, cancellationToken);
    }

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
            entity.HasMany(vacancy => vacancy.EmailTemplates)
                .WithOne()
                .HasForeignKey(template => template.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(vacancy => vacancy.EmailTemplates)
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
            entity.Property(candidate => candidate.HireOutcome).HasConversion<string>();
            entity.Property(candidate => candidate.ExtractionStatus).HasConversion<string>();
            entity.Property(candidate => candidate.IntakeSource).HasConversion<string>();
            entity.Property(candidate => candidate.FullNameProvenance).HasConversion<string>();
            entity.Property(candidate => candidate.ContactEmailProvenance).HasConversion<string>();
            entity.Property(candidate => candidate.ContactPhoneProvenance).HasConversion<string>();
            entity.Property(candidate => candidate.IsResubmitted);
            entity.Property(candidate => candidate.ScreenedOut);
            entity.Property(candidate => candidate.ScreeningVerdict)
                .HasConversion(
                    verdict => verdict == null ? null : JsonSerializer.Serialize(verdict),
                    json => json == null
                        ? null
                        : JsonSerializer.Deserialize<ScreeningRuleMatch[]>(json));
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
            entity.HasMany(candidate => candidate.FormResponses)
                .WithOne()
                .HasForeignKey(response => response.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(candidate => candidate.FormResponses)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CandidateFormResponse>(entity =>
        {
            entity.HasKey(response => response.Id);
            entity.Property(response => response.Id).ValueGeneratedOnAdd();
            entity.Property(response => response.Cells)
                .HasConversion(
                    cells => JsonSerializer.Serialize(cells),
                    json => JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>());
            entity.Property(response => response.FormTimestampParsed)
                .HasConversion(nullableDateTimeOffsetConverter);
            entity.Property(response => response.ImportedAt)
                .HasConversion(dateTimeOffsetConverter);
            entity.HasOne<Candidate>()
                .WithMany(candidate => candidate.FormResponses)
                .HasForeignKey(response => response.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Id).ValueGeneratedOnAdd();
            entity.Property(template => template.Kind).HasConversion<string>();
            entity.HasOne<Vacancy>()
                .WithMany(vacancy => vacancy.EmailTemplates)
                .HasForeignKey(template => template.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormLayout>(entity =>
        {
            entity.HasKey(layout => layout.Id);
            entity.Property(layout => layout.Id).ValueGeneratedOnAdd();
            entity.Property(layout => layout.HeaderSnapshot)
                .HasConversion(
                    headers => JsonSerializer.Serialize(headers),
                    json => JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>());
            entity.Property(layout => layout.Columns)
                .HasConversion(
                    columns => JsonSerializer.Serialize(columns),
                    json => JsonSerializer.Deserialize<FormLayoutColumn[]>(json) ?? Array.Empty<FormLayoutColumn>());
            entity.HasOne<Vacancy>()
                .WithOne(vacancy => vacancy.FormLayout)
                .HasForeignKey<FormLayout>(layout => layout.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScreeningRuleSet>(entity =>
        {
            entity.HasKey(ruleSet => ruleSet.Id);
            entity.Property(ruleSet => ruleSet.Id).ValueGeneratedOnAdd();
            entity.Property(ruleSet => ruleSet.Rules)
                .HasConversion(
                    rules => JsonSerializer.Serialize(rules),
                    json => JsonSerializer.Deserialize<ScreeningRule[]>(json) ?? Array.Empty<ScreeningRule>());
            entity.HasOne<Vacancy>()
                .WithOne(vacancy => vacancy.ScreeningRuleSet)
                .HasForeignKey<ScreeningRuleSet>(ruleSet => ruleSet.VacancyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}