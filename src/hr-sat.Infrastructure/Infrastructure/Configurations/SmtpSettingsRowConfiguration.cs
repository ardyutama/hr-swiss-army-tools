using hr_sat.Application.Abstractions.Email;
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
        {
            table.HasCheckConstraint("smtp_settings_singleton_check", "id = 1");
            table.HasCheckConstraint(
                "smtp_settings_sign_in_method_check",
                "sign_in_method IN ('app-password', 'microsoft-account')");
        });

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
        // Kebab-case wire values in the column (repo enum-column precedent), so the
        // check constraint reads in glossary terms (issue 03, decisions 16, 28).
        entity.Property(row => row.SignInMethod)
            .HasColumnName("sign_in_method")
            .HasColumnType("text")
            .HasConversion(
                method => method.ToApiValue(),
                value => ParseSignInMethod(value))
            .IsRequired();
        entity.Property(row => row.ProtectedPassword)
            .HasColumnName("protected_password")
            .HasColumnType("text");
        entity.Property(row => row.ProtectedGrant)
            .HasColumnName("protected_grant")
            .HasColumnType("text");
        entity.Property(row => row.FromAddress)
            .HasColumnName("from_address")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(row => row.FromName)
            .HasColumnName("from_name")
            .HasColumnType("text");
    }

    // The column only ever holds the check constraint's two values; an unknown value
    // is data corruption, not user input, so this fails loudly.
    private static SignInMethod ParseSignInMethod(string value) =>
        SignInMethodExtensions.TryParse(value, out var method)
            ? method
            : throw new InvalidOperationException($"Unknown sign-in method '{value}'.");
}
