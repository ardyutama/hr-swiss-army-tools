using hr_sat.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class SmtpSettingsRowConfiguration : IEntityTypeConfiguration<SmtpSettingsRow>
{
    public void Configure(EntityTypeBuilder<SmtpSettingsRow> entity)
    {
        // One SMTP Account per installation (CONTEXT.md): the singleton row always
        // carries id 1; the check constraint is the database backstop.
        entity.ToTable("smtp_settings", table =>
            table.HasCheckConstraint("smtp_settings_singleton_check", "id = 1"));

        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        entity.Property(row => row.Host)
            .HasColumnName("host")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(row => row.Port)
            .HasColumnName("port")
            .IsRequired();
        entity.Property(row => row.Username)
            .HasColumnName("username")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(row => row.ProtectedPassword)
            .HasColumnName("protected_password")
            .HasColumnType("text");
        entity.Property(row => row.FromAddress)
            .HasColumnName("from_address")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(row => row.FromName)
            .HasColumnName("from_name")
            .HasColumnType("text");
    }
}
