using hr_sat.Server.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Server.Infrastructure.Configurations;

internal sealed class VacancyRequirementConfiguration : IEntityTypeConfiguration<VacancyRequirement>
{
    public void Configure(EntityTypeBuilder<VacancyRequirement> entity)
    {
        entity.ToTable("vacancy_requirement", table =>
        {
            table.HasCheckConstraint(
                "vacancy_requirement_phrase_check",
                "char_length(btrim(phrase)) BETWEEN 1 AND 200");
            table.HasCheckConstraint(
                "vacancy_requirement_position_check",
                "position >= 1");
        });

        entity.HasKey(requirement => requirement.Id);
        entity.Property(requirement => requirement.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(requirement => requirement.VacancyId)
            .HasColumnName("vacancy_id");
        entity.Property(requirement => requirement.Phrase)
            .HasColumnName("phrase")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(requirement => requirement.PhraseNormalized)
            .HasColumnName("phrase_normalized")
            .HasColumnType("text")
            .HasComputedColumnSql("lower(btrim(phrase))", stored: true);
        entity.Property(requirement => requirement.Position)
            .HasColumnName("position")
            .IsRequired();

        entity.HasIndex(requirement => new { requirement.VacancyId, requirement.Position })
            .IsUnique()
            .HasDatabaseName("vacancy_requirement_vacancy_position_key");
        entity.HasIndex(requirement => new { requirement.VacancyId, requirement.PhraseNormalized })
            .IsUnique()
            .HasDatabaseName("vacancy_requirement_vacancy_phrase_key");
    }
}