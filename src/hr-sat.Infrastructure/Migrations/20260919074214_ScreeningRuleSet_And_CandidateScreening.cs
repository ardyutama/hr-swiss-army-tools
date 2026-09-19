using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScreeningRuleSet_And_CandidateScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "screened_out",
                table: "candidate",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "screening_verdict",
                table: "candidate",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "screening_rule_set",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    rules = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_rule_set", x => x.id);
                    table.CheckConstraint("screening_rule_set_vacancy_id_check", "vacancy_id > 0");
                    table.ForeignKey(
                        name: "FK_screening_rule_set_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "screening_rule_set_vacancy_key",
                table: "screening_rule_set",
                column: "vacancy_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "screening_rule_set");

            migrationBuilder.DropColumn(
                name: "screened_out",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "screening_verdict",
                table: "candidate");
        }
    }
}
