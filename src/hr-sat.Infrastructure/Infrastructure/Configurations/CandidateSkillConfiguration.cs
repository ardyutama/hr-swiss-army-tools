using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    public void Configure(EntityTypeBuilder<CandidateSkill> entity)
    {
        entity.ToTable("candidate_skill", table =>
        {
            table.HasCheckConstraint(
                "candidate_skill_phrase_check",
                "char_length(btrim(phrase)) BETWEEN 1 AND 200");
            table.HasCheckConstraint(
                "candidate_skill_position_check",
                "position >= 1");
        });

        entity.HasKey(skill => skill.Id);
        entity.Property(skill => skill.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(skill => skill.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();
        entity.Property(skill => skill.Phrase)
            .HasColumnName("phrase")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(skill => skill.PhraseNormalized)
            .HasColumnName("phrase_normalized")
            .HasColumnType("text")
            .HasComputedColumnSql("lower(btrim(phrase))", stored: true);
        entity.Property(skill => skill.Position)
            .HasColumnName("position")
            .IsRequired();

        entity.HasIndex(skill => new { skill.CandidateId, skill.Position })
            .IsUnique()
            .HasDatabaseName("candidate_skill_candidate_position_key");
        entity.HasIndex(skill => new { skill.CandidateId, skill.PhraseNormalized })
            .IsUnique()
            .HasDatabaseName("candidate_skill_candidate_phrase_key");
    }
}
