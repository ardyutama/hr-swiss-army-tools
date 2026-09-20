using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Dispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_run",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    intake_round_id = table.Column<long>(type: "bigint", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_run", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_run_intake_round_intake_round_id",
                        column: x => x.intake_round_id,
                        principalTable: "intake_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_run_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispatch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    dispatch_run_id = table.Column<long>(type: "bigint", nullable: false),
                    candidate_id = table.Column<long>(type: "bigint", nullable: false),
                    template_kind = table.Column<string>(type: "text", nullable: false),
                    rendered_subject = table.Column<string>(type: "text", nullable: false),
                    rendered_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch", x => x.id);
                    table.CheckConstraint("dispatch_status_check", "status IN ('sent', 'failed')");
                    table.ForeignKey(
                        name: "FK_dispatch_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_dispatch_run_dispatch_run_id",
                        column: x => x.dispatch_run_id,
                        principalTable: "dispatch_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "dispatch_candidate_sent_key",
                table: "dispatch",
                column: "candidate_id",
                unique: true,
                filter: "status = 'sent'");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_dispatch_run_id",
                table: "dispatch",
                column: "dispatch_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_run_intake_round_id",
                table: "dispatch_run",
                column: "intake_round_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_run_vacancy_id",
                table: "dispatch_run",
                column: "vacancy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch");

            migrationBuilder.DropTable(
                name: "dispatch_run");
        }
    }
}
