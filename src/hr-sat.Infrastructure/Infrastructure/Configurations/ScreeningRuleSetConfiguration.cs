using System.Text.Json;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class ScreeningRuleSetConfiguration : IEntityTypeConfiguration<ScreeningRuleSet>
{
    public void Configure(EntityTypeBuilder<ScreeningRuleSet> entity)
    {
        entity.ToTable("screening_rule_set", table =>
        {
            table.HasCheckConstraint(
                "screening_rule_set_vacancy_id_check",
                "vacancy_id > 0");
        });

        entity.HasKey(ruleSet => ruleSet.Id);
        entity.Property(ruleSet => ruleSet.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(ruleSet => ruleSet.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(ruleSet => ruleSet.Rules)
            .HasColumnName("rules")
            .HasColumnType("jsonb")
            .HasConversion(
                rules => JsonSerializer.Serialize(rules),
                json => JsonSerializer.Deserialize<ScreeningRule[]>(json) ?? Array.Empty<ScreeningRule>())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<ScreeningRule>>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                value => value.Aggregate(0, (hash, rule) => HashCode.Combine(hash, rule.GetHashCode())),
                value => value.ToArray()));

        entity.HasOne<Vacancy>()
            .WithOne(vacancy => vacancy.ScreeningRuleSet)
            .HasForeignKey<ScreeningRuleSet>(ruleSet => ruleSet.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(ruleSet => ruleSet.VacancyId)
            .IsUnique()
            .HasDatabaseName("screening_rule_set_vacancy_key");
    }
}