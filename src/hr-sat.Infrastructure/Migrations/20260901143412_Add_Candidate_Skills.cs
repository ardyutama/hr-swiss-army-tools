using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Candidate_Skills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_skill",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    candidate_id = table.Column<long>(type: "bigint", nullable: false),
                    phrase = table.Column<string>(type: "text", nullable: false),
                    phrase_normalized = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(btrim(phrase))", stored: true),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_skill", x => x.id);
                    table.CheckConstraint("candidate_skill_phrase_check", "char_length(btrim(phrase)) BETWEEN 1 AND 200");
                    table.CheckConstraint("candidate_skill_position_check", "position >= 1");
                    table.ForeignKey(
                        name: "FK_candidate_skill_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "candidate_skill_candidate_phrase_key",
                table: "candidate_skill",
                columns: new[] { "candidate_id", "phrase_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "candidate_skill_candidate_position_key",
                table: "candidate_skill",
                columns: new[] { "candidate_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_skill");
        }
    }
}
