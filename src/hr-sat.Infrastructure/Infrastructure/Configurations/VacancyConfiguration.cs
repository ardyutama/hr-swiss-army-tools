using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> entity)
    {
        entity.ToTable("vacancy", table =>
        {
            table.HasCheckConstraint(
                "vacancy_title_check",
                "char_length(btrim(title)) BETWEEN 1 AND 200");
            table.HasCheckConstraint(
                "vacancy_status_check",
                "status IN ('open', 'closed')");
            table.HasCheckConstraint(
                "vacancy_closed_at_check",
                "(status = 'open' AND closed_at IS NULL) OR " +
                "(status = 'closed' AND closed_at IS NOT NULL)");
        });

        entity.HasKey(vacancy => vacancy.Id);
        entity.Property(vacancy => vacancy.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(vacancy => vacancy.Title)
            .HasColumnName("title")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(vacancy => vacancy.OpenedOn)
            .HasColumnName("opened_on")
            .IsRequired();
        entity.Property(vacancy => vacancy.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .HasConversion(
                status => status == VacancyStatus.Open ? "open" : "closed",
                value => value == "open" ? VacancyStatus.Open : VacancyStatus.Closed)
            .HasDefaultValue(VacancyStatus.Open)
            .IsRequired();
        entity.Property(vacancy => vacancy.ClosedAt)
            .HasColumnName("closed_at");
        entity.Property(vacancy => vacancy.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        entity.HasMany(vacancy => vacancy.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(vacancy => vacancy.Requirements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}