using hr_sat.Domain.Candidates;
using hr_sat.Domain.Dispatches;
using hr_sat.Domain.EmailTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
{
    public void Configure(EntityTypeBuilder<Dispatch> entity)
    {
        entity.ToTable("dispatch", table =>
        {
            table.HasCheckConstraint(
                "dispatch_status_check",
                "status IN ('sent', 'failed')");
        });

        entity.HasKey(dispatch => dispatch.Id);
        entity.Property(dispatch => dispatch.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(dispatch => dispatch.DispatchRunId)
            .HasColumnName("dispatch_run_id")
            .IsRequired();
        entity.Property(dispatch => dispatch.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();
        entity.Property(dispatch => dispatch.TemplateKind)
            .HasColumnName("template_kind")
            .HasColumnType("text")
            .HasConversion(
                kind => kind.ToString().ToLowerInvariant(),
                value => Enum.Parse<EmailTemplateKind>(value, ignoreCase: true))
            .IsRequired();
        entity.Property(dispatch => dispatch.RenderedSubject)
            .HasColumnName("rendered_subject")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(dispatch => dispatch.RenderedBody)
            .HasColumnName("rendered_body")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(dispatch => dispatch.FromAddress)
            .HasColumnName("from_address")
            .HasColumnType("text")
            .IsRequired();
        entity.Property(dispatch => dispatch.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<DispatchStatus>(value, ignoreCase: true))
            .IsRequired();
        entity.Property(dispatch => dispatch.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");
        entity.Property(dispatch => dispatch.AttemptedAt)
            .HasColumnName("attempted_at")
            .IsRequired();

        entity.HasOne<DispatchRun>()
            .WithMany()
            .HasForeignKey(dispatch => dispatch.DispatchRunId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(dispatch => dispatch.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Database backstop of the idempotency rule: a successful Dispatch per
        // Candidate row is recorded once, ever (23505 on a racing insert is caught
        // and counted as already-sent, never as a failure).
        entity.HasIndex(dispatch => dispatch.CandidateId)
            .IsUnique()
            .HasFilter("status = 'sent'")
            .HasDatabaseName("dispatch_candidate_sent_key");
    }
}
