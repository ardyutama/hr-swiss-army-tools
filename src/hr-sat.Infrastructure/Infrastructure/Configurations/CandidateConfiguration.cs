using System.Text.Json;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

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
                "candidate_hire_outcome_check",
                "hire_outcome IN ('none', 'hired', 'runaway', 'declined')");
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
                "candidate_intake_source_check",
                "intake_source IN ('email', 'form')");
            table.HasCheckConstraint(
                "candidate_contact_phone_check",
                "contact_phone IS NULL OR char_length(btrim(contact_phone)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "candidate_full_name_provenance_check",
                "full_name_provenance IN ('none', 'formprefilled', 'typed')");
            table.HasCheckConstraint(
                "candidate_contact_email_provenance_check",
                "contact_email_provenance IN ('none', 'formprefilled', 'typed')");
            table.HasCheckConstraint(
                "candidate_contact_phone_provenance_check",
                "contact_phone_provenance IN ('none', 'formprefilled', 'typed')");
            table.HasCheckConstraint(
                "candidate_source_sender_name_check",
                "source_sender_name IS NULL OR char_length(btrim(source_sender_name)) BETWEEN 1 AND 300");
            table.HasCheckConstraint(
                "candidate_source_sender_email_check",
                "source_sender_email IS NULL OR char_length(btrim(source_sender_email)) BETWEEN 1 AND 320");
            table.HasCheckConstraint(
                "candidate_source_original_filename_check",
                "intake_source = 'form' OR (source_original_filename IS NOT NULL AND btrim(source_original_filename) <> '')");
            table.HasCheckConstraint(
                "candidate_source_storage_key_check",
                "intake_source = 'form' OR (source_storage_key IS NOT NULL AND btrim(source_storage_key) <> '')");
            table.HasCheckConstraint(
                "candidate_source_size_bytes_check",
                "intake_source = 'form' OR (source_size_bytes IS NOT NULL AND source_size_bytes > 0)");
            table.HasCheckConstraint(
                "candidate_source_sha256_check",
                "intake_source = 'form' OR (source_sha256 IS NOT NULL AND octet_length(source_sha256) = 32)");
        });

        entity.HasKey(candidate => candidate.Id);
        entity.Property(candidate => candidate.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(candidate => candidate.IntakeRoundId)
            .HasColumnName("intake_round_id")
            .IsRequired();
        entity.Property(candidate => candidate.IntakeSource)
            .HasColumnName("intake_source")
            .HasColumnType("text")
            .HasConversion(
                source => ToDatabaseValue(source),
                value => FromDatabaseValue<CandidateIntakeSource>(value))
            .HasDefaultValue(CandidateIntakeSource.Email)
            .IsRequired();
        entity.Property(candidate => candidate.IsResubmitted)
            .HasColumnName("is_resubmitted")
            .HasDefaultValue(false)
            .IsRequired();
        entity.Property(candidate => candidate.ScreenedOut)
            .HasColumnName("screened_out")
            .HasDefaultValue(false)
            .IsRequired();
        entity.Property(candidate => candidate.ScreeningVerdict)
            .HasColumnName("screening_verdict")
            .HasColumnType("jsonb")
            .HasConversion(
                verdict => verdict == null ? null : JsonSerializer.Serialize(verdict),
                json => json == null
                    ? null
                    : JsonSerializer.Deserialize<ScreeningRuleMatch[]>(json))
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<ScreeningRuleMatch>?>(
                (left, right) => left == null && right == null ||
                    left != null && right != null && left.SequenceEqual(right),
                value => value == null
                    ? 0
                    : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                value => value == null ? null : value.ToArray()));
        entity.Property(candidate => candidate.PromotedFromRoundNumber)
            .HasColumnName("promoted_from_round_number");
        entity.Property(candidate => candidate.PromotedAt)
            .HasColumnName("promoted_at");
        entity.Property(candidate => candidate.ReviewStatus)
            .HasColumnName("review_status")
            .HasColumnType("text")
            .HasConversion(
                status => ToDatabaseValue(status),
                value => FromDatabaseValue<CandidateReviewStatus>(value))
            .HasDefaultValue(CandidateReviewStatus.New)
            .IsRequired();
        entity.Property(candidate => candidate.HireOutcome)
            .HasColumnName("hire_outcome")
            .HasColumnType("text")
            .HasConversion(
                outcome => ToDatabaseValue(outcome),
                value => FromDatabaseValue<CandidateHireOutcome>(value))
            .HasDefaultValue(CandidateHireOutcome.None)
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
        entity.Property(candidate => candidate.FullNameProvenance)
            .HasColumnName("full_name_provenance")
            .HasColumnType("text")
            .HasConversion(
                provenance => ToDatabaseValue(provenance),
                value => FromDatabaseValue<CandidateDetailProvenance>(value))
            .HasDefaultValue(CandidateDetailProvenance.None)
            .IsRequired();
        entity.Property(candidate => candidate.ContactEmailProvenance)
            .HasColumnName("contact_email_provenance")
            .HasColumnType("text")
            .HasConversion(
                provenance => ToDatabaseValue(provenance),
                value => FromDatabaseValue<CandidateDetailProvenance>(value))
            .HasDefaultValue(CandidateDetailProvenance.None)
            .IsRequired();
        entity.Property(candidate => candidate.ContactPhoneProvenance)
            .HasColumnName("contact_phone_provenance")
            .HasColumnType("text")
            .HasConversion(
                provenance => ToDatabaseValue(provenance),
                value => FromDatabaseValue<CandidateDetailProvenance>(value))
            .HasDefaultValue(CandidateDetailProvenance.None)
            .IsRequired();
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
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceStorageKey)
            .HasColumnName("source_storage_key")
            .HasColumnType("text");
        entity.Property(candidate => candidate.SourceSizeBytes)
            .HasColumnName("source_size_bytes");
        entity.Property(candidate => candidate.SourceSha256)
            .HasColumnName("source_sha256")
            .HasColumnType("bytea");
        entity.Property(candidate => candidate.ImportedAt)
            .HasColumnName("imported_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        entity.HasOne<IntakeRound>()
            .WithMany(round => round.Candidates)
            .HasForeignKey(candidate => candidate.IntakeRoundId)
            .OnDelete(DeleteBehavior.Cascade);
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

        entity.HasIndex(candidate => candidate.SourceStorageKey)
            .IsUnique()
            .HasFilter("source_storage_key IS NOT NULL")
            .HasDatabaseName("candidate_source_storage_key_key");
        entity.HasIndex(candidate => new { candidate.IntakeRoundId, candidate.SourceSha256 })
            .IsUnique()
            .HasDatabaseName("candidate_round_source_sha256_key");
        entity.HasIndex(candidate => new { candidate.IntakeRoundId, candidate.ImportedAt, candidate.Id })
            .HasDatabaseName("candidate_round_imported_idx");
    }

    private static string ToDatabaseValue<TStatus>(TStatus status)
        where TStatus : struct, Enum => status.ToString().ToLowerInvariant();

    private static TStatus FromDatabaseValue<TStatus>(string value)
        where TStatus : struct, Enum => Enum.Parse<TStatus>(value, ignoreCase: true);
}