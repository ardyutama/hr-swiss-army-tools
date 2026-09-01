using hr_sat.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class PendingFileDeletionConfiguration : IEntityTypeConfiguration<PendingFileDeletion>
{
    public void Configure(EntityTypeBuilder<PendingFileDeletion> entity)
    {
        entity.ToTable("pending_file_deletion", table =>
        {
            table.HasCheckConstraint(
                "pending_file_deletion_storage_key_check",
                "btrim(storage_key) <> ''");
        });

        entity.HasKey(deletion => deletion.Id);
        entity.Property(deletion => deletion.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(deletion => deletion.StorageKey)
            .HasColumnName("storage_key")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(deletion => deletion.EnqueuedAt)
            .HasColumnName("enqueued_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        entity.HasIndex(deletion => deletion.StorageKey)
            .HasDatabaseName("pending_file_deletion_storage_key_idx");
    }
}
