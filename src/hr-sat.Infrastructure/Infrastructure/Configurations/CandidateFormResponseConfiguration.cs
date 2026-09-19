using System.Text.Json;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Candidates.FormResponses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class CandidateFormResponseConfiguration : IEntityTypeConfiguration<CandidateFormResponse>
{
    public void Configure(EntityTypeBuilder<CandidateFormResponse> entity)
    {
        entity.ToTable("candidate_form_response", table =>
        {
            table.HasCheckConstraint(
                "candidate_form_response_candidate_id_check",
                "candidate_id > 0");
            table.HasCheckConstraint(
                "candidate_form_response_cells_check",
                "jsonb_array_length(cells) > 0");
        });

        entity.HasKey(response => response.Id);
        entity.Property(response => response.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(response => response.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();
        entity.Property(response => response.Cells)
            .HasColumnName("cells")
            .HasColumnType("jsonb")
            .HasConversion(
                cells => JsonSerializer.Serialize(cells),
                json => JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>())
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                value => value.Aggregate(0, (hash, cell) => HashCode.Combine(hash, cell.GetHashCode())),
                value => value.ToArray()));
        entity.Property(response => response.FormTimestampRaw)
            .HasColumnName("form_timestamp_raw")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(response => response.FormTimestampParsed)
            .HasColumnName("form_timestamp_parsed");
        entity.Property(response => response.IdentityKey)
            .HasColumnName("identity_key")
            .HasColumnType("text");
        entity.Property(response => response.IsCurrent)
            .HasColumnName("is_current")
            .HasDefaultValue(true)
            .IsRequired();
        entity.Property(response => response.ImportedAt)
            .HasColumnName("imported_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        entity.HasOne<Candidate>()
            .WithMany(candidate => candidate.FormResponses)
            .HasForeignKey(response => response.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(response => response.CandidateId)
            .IsUnique()
            .HasFilter("is_current")
            .HasDatabaseName("candidate_form_response_current_key");
        entity.HasIndex(response => response.IdentityKey)
            .HasFilter("identity_key IS NOT NULL")
            .HasDatabaseName("candidate_form_response_identity_idx");
    }
}