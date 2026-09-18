using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FormLayoutColumns_And_CandidateProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email_column_ordinal",
                table: "form_layout");

            migrationBuilder.DropColumn(
                name: "contact_phone_column_ordinal",
                table: "form_layout");

            migrationBuilder.DropColumn(
                name: "cv_link_column_ordinal",
                table: "form_layout");

            migrationBuilder.DropColumn(
                name: "name_column_ordinal",
                table: "form_layout");

            migrationBuilder.AddColumn<string>(
                name: "columns",
                table: "form_layout",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "contact_email_provenance",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "contact_phone_provenance",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "full_name_provenance",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_contact_email_provenance_check",
                table: "candidate",
                sql: "contact_email_provenance IN ('none', 'formprefilled', 'typed')");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_contact_phone_provenance_check",
                table: "candidate",
                sql: "contact_phone_provenance IN ('none', 'formprefilled', 'typed')");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_full_name_provenance_check",
                table: "candidate",
                sql: "full_name_provenance IN ('none', 'formprefilled', 'typed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "candidate_contact_email_provenance_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_contact_phone_provenance_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_full_name_provenance_check",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "columns",
                table: "form_layout");

            migrationBuilder.DropColumn(
                name: "contact_email_provenance",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "contact_phone_provenance",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "full_name_provenance",
                table: "candidate");

            migrationBuilder.AddColumn<int>(
                name: "contact_email_column_ordinal",
                table: "form_layout",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contact_phone_column_ordinal",
                table: "form_layout",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cv_link_column_ordinal",
                table: "form_layout",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "name_column_ordinal",
                table: "form_layout",
                type: "integer",
                nullable: true);
        }
    }
}
