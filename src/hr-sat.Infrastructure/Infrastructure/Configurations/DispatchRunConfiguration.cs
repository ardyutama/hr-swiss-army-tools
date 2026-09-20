using hr_sat.Domain.Dispatches;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class DispatchRunConfiguration : IEntityTypeConfiguration<DispatchRun>
{
    public void Configure(EntityTypeBuilder<DispatchRun> entity)
    {
        entity.ToTable("dispatch_run");

        entity.HasKey(run => run.Id);
        entity.Property(run => run.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(run => run.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(run => run.IntakeRoundId)
            .HasColumnName("intake_round_id")
            .IsRequired();
        entity.Property(run => run.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();
        entity.Property(run => run.CompletedAt)
            .HasColumnName("completed_at");

        entity.HasOne<Vacancy>()
            .WithMany()
            .HasForeignKey(run => run.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<IntakeRound>()
            .WithMany()
            .HasForeignKey(run => run.IntakeRoundId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(run => run.IntakeRoundId)
            .HasDatabaseName("ix_dispatch_run_intake_round_id");
    }
}
