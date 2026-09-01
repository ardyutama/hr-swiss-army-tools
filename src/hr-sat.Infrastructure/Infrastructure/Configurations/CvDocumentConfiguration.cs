using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class CvDocumentConfiguration : IEntityTypeConfiguration<CvDocument>
{
    public void Configure(EntityTypeBuilder<CvDocument> entity)
    {
        entity.ToTable("cv_document", table =>
        {
            table.HasCheckConstraint(
                "cv_document_position_check",
                "position >= 1");
            table.HasCheckConstraint(
                "cv_document_original_filename_check",
                "btrim(original_filename) <> ''");
            table.HasCheckConstraint(
                "cv_document_storage_key_check",
                "btrim(storage_key) <> ''");
            table.HasCheckConstraint(
                "cv_document_size_bytes_check",
                "size_bytes > 0");
            table.HasCheckConstraint(
                "cv_document_sha256_check",
                "octet_length(sha256) = 32");
        });

        entity.HasKey(document => document.Id);
        entity.Property(document => document.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(document => document.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();
        entity.Property(document => document.Position)
            .HasColumnName("position")
            .IsRequired();
        entity.Property(document => document.IsPrimary)
            .HasColumnName("is_primary")
            .HasDefaultValue(false)
            .IsRequired();
        entity.Property(document => document.OriginalFilename)
            .HasColumnName("original_filename")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(document => document.StorageKey)
            .HasColumnName("storage_key")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(document => document.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();
        entity.Property(document => document.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("bytea")
            .IsRequired();

        entity.HasIndex(document => document.StorageKey)
            .IsUnique()
            .HasDatabaseName("cv_document_storage_key_key");
        entity.HasIndex(document => new { document.CandidateId, document.Position })
            .IsUnique()
            .HasDatabaseName("cv_document_candidate_position_key");
        entity.HasIndex(document => document.CandidateId)
            .IsUnique()
            .HasDatabaseName("cv_document_candidate_primary_idx")
            .HasFilter("is_primary");
    }
}