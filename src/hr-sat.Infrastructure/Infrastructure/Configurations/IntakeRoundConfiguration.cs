using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class IntakeRoundConfiguration : IEntityTypeConfiguration<IntakeRound>
{
    public void Configure(EntityTypeBuilder<IntakeRound> entity)
    {
        entity.ToTable("intake_round", table =>
        {
            table.HasCheckConstraint(
                "intake_round_number_check",
                "round_number >= 1");
            table.HasCheckConstraint(
                "intake_round_name_check",
                "name IS NULL OR char_length(btrim(name)) BETWEEN 1 AND 200");
        });

        entity.HasKey(round => round.Id);
        entity.Property(round => round.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(round => round.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(round => round.RoundNumber)
            .HasColumnName("round_number")
            .IsRequired();
        entity.Property(round => round.Name)
            .HasColumnName("name")
            .HasColumnType("text");
        entity.Property(round => round.ClosedAt)
            .HasColumnName("closed_at");

        entity.HasOne<Vacancy>()
            .WithMany(vacancy => vacancy.Rounds)
            .HasForeignKey(round => round.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(round => round.Candidates)
            .WithOne()
            .HasForeignKey(candidate => candidate.IntakeRoundId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(round => round.Candidates)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.HasIndex(round => new { round.VacancyId, round.RoundNumber })
            .IsUnique()
            .HasDatabaseName("intake_round_vacancy_number_key");
        entity.HasIndex(round => round.VacancyId)
            .IsUnique()
            .HasFilter("closed_at IS NULL")
            .HasDatabaseName("intake_round_vacancy_active_key");
    }
}