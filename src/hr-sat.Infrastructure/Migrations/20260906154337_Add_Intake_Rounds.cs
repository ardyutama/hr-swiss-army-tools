using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Intake_Rounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intake_round",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_round", x => x.id);
                    table.CheckConstraint("intake_round_name_check", "name IS NULL OR char_length(btrim(name)) BETWEEN 1 AND 200");
                    table.CheckConstraint("intake_round_number_check", "round_number >= 1");
                    table.ForeignKey(
                        name: "FK_intake_round_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<long>(
                name: "intake_round_id",
                table: "candidate",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO intake_round (vacancy_id, round_number, name, closed_at)
                SELECT id, 1, NULL, NULL
                FROM vacancy;
                """);

            migrationBuilder.Sql(
                """
                UPDATE candidate AS candidate
                SET intake_round_id = round.id
                FROM intake_round AS round
                WHERE round.vacancy_id = candidate.vacancy_id
                  AND round.round_number = 1;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "intake_round_id",
                table: "candidate",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_candidate_vacancy_vacancy_id",
                table: "candidate");

            migrationBuilder.DropIndex(
                name: "candidate_vacancy_imported_idx",
                table: "candidate");

            migrationBuilder.DropIndex(
                name: "candidate_vacancy_source_sha256_key",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "vacancy_id",
                table: "candidate");

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_intake_round_intake_round_id",
                table: "candidate",
                column: "intake_round_id",
                principalTable: "intake_round",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "intake_round_vacancy_active_key",
                table: "intake_round",
                column: "vacancy_id",
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "intake_round_vacancy_number_key",
                table: "intake_round",
                columns: new[] { "vacancy_id", "round_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "candidate_round_imported_idx",
                table: "candidate",
                columns: new[] { "intake_round_id", "imported_at", "id" });

            migrationBuilder.CreateIndex(
                name: "candidate_round_source_sha256_key",
                table: "candidate",
                columns: new[] { "intake_round_id", "source_sha256" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidate_intake_round_intake_round_id",
                table: "candidate");

            migrationBuilder.DropIndex(
                name: "candidate_round_imported_idx",
                table: "candidate");

            migrationBuilder.DropIndex(
                name: "candidate_round_source_sha256_key",
                table: "candidate");

            migrationBuilder.AddColumn<long>(
                name: "vacancy_id",
                table: "candidate",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE candidate AS candidate
                SET vacancy_id = round.vacancy_id
                FROM intake_round AS round
                WHERE round.id = candidate.intake_round_id;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "vacancy_id",
                table: "candidate",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "intake_round_id",
                table: "candidate");

            migrationBuilder.DropTable(
                name: "intake_round");

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_vacancy_vacancy_id",
                table: "candidate",
                column: "vacancy_id",
                principalTable: "vacancy",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "candidate_vacancy_imported_idx",
                table: "candidate",
                columns: new[] { "vacancy_id", "imported_at", "id" });

            migrationBuilder.CreateIndex(
                name: "candidate_vacancy_source_sha256_key",
                table: "candidate",
                columns: new[] { "vacancy_id", "source_sha256" },
                unique: true);
        }
    }
}
