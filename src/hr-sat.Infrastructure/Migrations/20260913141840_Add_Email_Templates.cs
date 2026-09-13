using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Email_Templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_template",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_template", x => x.id);
                    table.CheckConstraint("email_template_body_check", "btrim(body) <> ''");
                    table.CheckConstraint("email_template_kind_check", "kind IN ('shortlisted', 'rejected')");
                    table.CheckConstraint("email_template_subject_check", "char_length(btrim(subject)) BETWEEN 1 AND 998");
                    table.ForeignKey(
                        name: "FK_email_template_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "email_template_vacancy_kind_key",
                table: "email_template",
                columns: new[] { "vacancy_id", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_template");
        }
    }
}
