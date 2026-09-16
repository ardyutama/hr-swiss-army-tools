using System.Text.Json;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hr_sat.Infrastructure.Configurations;

internal sealed class FormLayoutConfiguration : IEntityTypeConfiguration<FormLayout>
{
    public void Configure(EntityTypeBuilder<FormLayout> entity)
    {
        entity.ToTable("form_layout", table =>
        {
            table.HasCheckConstraint(
                "form_layout_vacancy_id_check",
                "vacancy_id > 0");
            table.HasCheckConstraint(
                "form_layout_header_snapshot_check",
                "jsonb_array_length(header_snapshot) > 0");
        });

        entity.HasKey(layout => layout.Id);
        entity.Property(layout => layout.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(layout => layout.VacancyId)
            .HasColumnName("vacancy_id")
            .IsRequired();
        entity.Property(layout => layout.HeaderSnapshot)
            .HasColumnName("header_snapshot")
            .HasColumnType("jsonb")
            .HasConversion(
                headers => JsonSerializer.Serialize(headers),
                json => JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>())
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                value => value.Aggregate(0, (hash, header) => HashCode.Combine(hash, header.GetHashCode())),
                value => value.ToArray()));
        entity.Property(layout => layout.NameColumnOrdinal)
            .HasColumnName("name_column_ordinal");
        entity.Property(layout => layout.ContactEmailColumnOrdinal)
            .HasColumnName("contact_email_column_ordinal");
        entity.Property(layout => layout.ContactPhoneColumnOrdinal)
            .HasColumnName("contact_phone_column_ordinal");
        entity.Property(layout => layout.CvLinkColumnOrdinal)
            .HasColumnName("cv_link_column_ordinal");

        entity.HasOne<Vacancy>()
            .WithOne(vacancy => vacancy.FormLayout)
            .HasForeignKey<FormLayout>(layout => layout.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(layout => layout.VacancyId)
            .IsUnique()
            .HasDatabaseName("form_layout_vacancy_key");
    }
}