using hr_sat.Server.Domain.Candidates;
using hr_sat.Server.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Server.Infrastructure.Configurations;

internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> entity)
    {
        entity.ToTable("candidate", table =>
        {
            table.HasCheckConstraint(
                "candidate_review_status_check",
                "review_status IN ('new', 'flagged', 'shortlisted', 'rejected')");
            table.HasCheckConstraint(
                "candidate_extraction_status_check",
                "extraction_status IN ('pending', 'succeeded', 'failed')");
            table.HasCheckConstraint(
                "candidate_full_name_check",
                "full_name IS NULL OR char_length(btrim(full_name)) BETWEEN 1 AND 300");
            table.HasCheckConstraint(
                "candidate_contact_email_check",
                "contact_email IS NULL OR char_length(btrim(contact_email)) BETWEEN 1 AND 320");
            table.HasCheckConstraint(
                "candidate_contact_phone_check",
                "contact_phone IS NULL OR char_length(btrim(contact_phone)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "candidate_source_sender_name_check",
                "source_sender_name IS NULL OR char_length(btrim(source_sender_name)) BETWEEN 1 AND 300");
            table.HasCheckConstraint(
                "candidate_source_sender_email_check",
                "source_sender_email IS NULL OR char_length(btrim(source_sender_email)) BETWEEN 1 AND 320");
            table.HasCheckConstraint(
                "candidate_source_original_filename_check",
                "btrim(source_original_filename) <> ''");
            table.HasCheckConstraint(
                "candidate_source_storage_key_check",
                "btrim(source_storage_key) <> ''");
            table.HasCheckConstraint(
                "candidate_source_size_bytes_check",
                "source_size_bytes > 0");
            table.HasCheckConstraint(
                "candidate_source_sha256_check",
                "octet_length(source_sha256) = 32");
        });

        entity.HasKey(candidate => candidate.Id);
        entity.Property(candidate => candidate.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(candidate => candidate.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(candidate => candidate.ReviewStatus)
            .HasColumnName("review_status")
            .HasColumnType("text")
            .HasConversion(
                status => ToDatabaseValue(status),
                value => FromDatabaseValue<CandidateReviewStatus>(value))
            .HasDefaultValue(CandidateReviewStatus.New)
            .IsRequired();
        entity.Property(candidate => candidate.ExtractionStatus)
            .HasColumnName("extraction_status")
            .HasColumnType("text")
            .HasConversion(
                status => ToDatabaseValue(status),
                value => FromDatabaseValue<CandidateExtractionStatus>(value))
            .HasDefaultValue(CandidateExtractionStatus.Pending)
            .IsRequired();
        entity.Property(candidate => candidate.FullName)
            .HasColumnName("full_name")
            .HasColumnType("text");
        entity.Property(candidate => candidate.ContactEmail)
            .HasColumnName("contact_email")
            .HasColumnType("text");
        entity.Property(candidate => candidate.ContactPhone)
            .HasColumnName("contact_phone")
            .HasColumnType("text");
        entity.Property(candidate => candidate.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceSenderName)
            .HasColumnName("source_sender_name")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceSenderEmail)
            .HasColumnName("source_sender_email")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceSubject)
            .HasColumnName("source_subject")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceBodyText)
            .HasColumnName("source_body_text")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceSentAt)
            .HasColumnName("source_sent_at");
        entity.Property(candidate => candidate.SourceOriginalFilename)
            .HasColumnName("source_original_filename")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(candidate => candidate.SourceStorageKey)
            .HasColumnName("source_storage_key")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(candidate => candidate.SourceSizeBytes)
            .HasColumnName("source_size_bytes")
            .IsRequired();
        entity.Property(candidate => candidate.SourceSha256)
            .HasColumnName("source_sha256")
            .HasColumnType("bytea")
            .IsRequired();
        entity.Property(candidate => candidate.ImportedAt)
            .HasColumnName("imported_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        entity.HasOne<Vacancy>()
            .WithMany(vacancy => vacancy.Candidates)
            .HasForeignKey(candidate => candidate.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(candidate => candidate.CvDocuments)
            .WithOne()
            .HasForeignKey(document => document.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(candidate => candidate.CvDocuments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasIndex(candidate => candidate.SourceStorageKey)
            .IsUnique()
            .HasDatabaseName("candidate_source_storage_key_key");
        entity.HasIndex(candidate => new { candidate.VacancyId, candidate.SourceSha256 })
            .IsUnique()
            .HasDatabaseName("candidate_vacancy_source_sha256_key");
        entity.HasIndex(candidate => new { candidate.VacancyId, candidate.ImportedAt, candidate.Id })
            .HasDatabaseName("candidate_vacancy_imported_idx");
    }

    private static string ToDatabaseValue<TStatus>(TStatus status)
        where TStatus : struct, Enum => status.ToString().ToLowerInvariant();

    private static TStatus FromDatabaseValue<TStatus>(string value)
        where TStatus : struct, Enum => Enum.Parse<TStatus>(value, ignoreCase: true);
}