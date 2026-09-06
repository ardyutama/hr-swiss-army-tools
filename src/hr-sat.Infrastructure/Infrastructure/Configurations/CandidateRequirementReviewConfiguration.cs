using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class CandidateRequirementReviewConfiguration
    : IEntityTypeConfiguration<CandidateRequirementReview>
{
    public void Configure(EntityTypeBuilder<CandidateRequirementReview> entity)
    {
        entity.ToTable("candidate_requirement_review", table =>
        {
            table.HasCheckConstraint(
                "candidate_requirement_review_candidate_id_check",
                "candidate_id > 0");
            table.HasCheckConstraint(
                "candidate_requirement_review_requirement_id_check",
                "vacancy_requirement_id > 0");
        });

        entity.HasKey(review => review.Id);
        entity.Property(review => review.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(review => review.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();
        entity.Property(review => review.VacancyRequirementId)
            .HasColumnName("vacancy_requirement_id")
            .IsRequired();
        entity.Property(review => review.Confirmed)
            .HasColumnName("confirmed")
            .IsRequired();

        entity.HasOne<VacancyRequirement>()
            .WithMany()
            .HasForeignKey(review => review.VacancyRequirementId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(review => new { review.CandidateId, review.VacancyRequirementId })
            .IsUnique()
            .HasDatabaseName("candidate_requirement_review_candidate_requirement_key");
    }
}
