using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> entity)
    {
        entity.ToTable("email_template", table =>
        {
            table.HasCheckConstraint(
                "email_template_kind_check",
                "kind IN ('shortlisted', 'rejected')");
            table.HasCheckConstraint(
                "email_template_subject_check",
                "char_length(btrim(subject)) BETWEEN 1 AND 998");
            table.HasCheckConstraint(
                "email_template_body_check",
                "btrim(body) <> ''");
        });

        entity.HasKey(template => template.Id);
        entity.Property(template => template.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(template => template.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(template => template.Kind)
            .HasColumnName("kind")
            .HasColumnType("text")
            .HasConversion(
                kind => kind.ToString().ToLowerInvariant(),
                value => Enum.Parse<EmailTemplateKind>(value, ignoreCase: true))
            .IsRequired();
        entity.Property(template => template.Subject)
            .HasColumnName("subject")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(template => template.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        entity.HasOne<Vacancy>()
            .WithMany(vacancy => vacancy.EmailTemplates)
            .HasForeignKey(template => template.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(template => new { template.VacancyId, template.Kind })
            .IsUnique()
            .HasDatabaseName("email_template_vacancy_kind_key");
    }
}