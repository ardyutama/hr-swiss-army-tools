using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewWorkspace_V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_requirement_review",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    candidate_id = table.Column<long>(type: "bigint", nullable: false),
                    vacancy_requirement_id = table.Column<long>(type: "bigint", nullable: false),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_requirement_review", x => x.id);
                    table.CheckConstraint("candidate_requirement_review_candidate_id_check", "candidate_id > 0");
                    table.CheckConstraint("candidate_requirement_review_requirement_id_check", "vacancy_requirement_id > 0");
                    table.ForeignKey(
                        name: "FK_candidate_requirement_review_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_requirement_review_vacancy_requirement_vacancy_re~",
                        column: x => x.vacancy_requirement_id,
                        principalTable: "vacancy_requirement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "candidate_requirement_review_candidate_requirement_key",
                table: "candidate_requirement_review",
                columns: new[] { "candidate_id", "vacancy_requirement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_requirement_review_vacancy_requirement_id",
                table: "candidate_requirement_review",
                column: "vacancy_requirement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_requirement_review");
        }
    }
}
