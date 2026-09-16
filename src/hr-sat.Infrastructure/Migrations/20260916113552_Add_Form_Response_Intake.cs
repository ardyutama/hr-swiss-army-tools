using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Form_Response_Intake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "candidate_source_storage_key_key",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_original_filename_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_sha256_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_size_bytes_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_storage_key_check",
                table: "candidate");

            migrationBuilder.AlterColumn<string>(
                name: "source_storage_key",
                table: "candidate",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "source_size_bytes",
                table: "candidate",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<byte[]>(
                name: "source_sha256",
                table: "candidate",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<string>(
                name: "source_original_filename",
                table: "candidate",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "intake_source",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "email");

            migrationBuilder.AddColumn<bool>(
                name: "is_resubmitted",
                table: "candidate",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "candidate_form_response",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    candidate_id = table.Column<long>(type: "bigint", nullable: false),
                    cells = table.Column<string>(type: "jsonb", nullable: false),
                    form_timestamp_raw = table.Column<string>(type: "text", nullable: false),
                    form_timestamp_parsed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identity_key = table.Column<string>(type: "text", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_form_response", x => x.id);
                    table.CheckConstraint("candidate_form_response_candidate_id_check", "candidate_id > 0");
                    table.CheckConstraint("candidate_form_response_cells_check", "jsonb_array_length(cells) > 0");
                    table.ForeignKey(
                        name: "FK_candidate_form_response_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "candidate_source_storage_key_key",
                table: "candidate",
                column: "source_storage_key",
                unique: true,
                filter: "source_storage_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_intake_source_check",
                table: "candidate",
                sql: "intake_source IN ('email', 'form')");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_original_filename_check",
                table: "candidate",
                sql: "intake_source = 'form' OR (source_original_filename IS NOT NULL AND btrim(source_original_filename) <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_sha256_check",
                table: "candidate",
                sql: "intake_source = 'form' OR (source_sha256 IS NOT NULL AND octet_length(source_sha256) = 32)");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_size_bytes_check",
                table: "candidate",
                sql: "intake_source = 'form' OR (source_size_bytes IS NOT NULL AND source_size_bytes > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_storage_key_check",
                table: "candidate",
                sql: "intake_source = 'form' OR (source_storage_key IS NOT NULL AND btrim(source_storage_key) <> '')");

            migrationBuilder.CreateIndex(
                name: "candidate_form_response_current_key",
                table: "candidate_form_response",
                column: "candidate_id",
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "candidate_form_response_identity_idx",
                table: "candidate_form_response",
                column: "identity_key",
                filter: "identity_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_form_response");

            migrationBuilder.DropIndex(
                name: "candidate_source_storage_key_key",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_intake_source_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_original_filename_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_sha256_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_size_bytes_check",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "candidate_source_storage_key_check",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "intake_source",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "is_resubmitted",
                table: "candidate");

            migrationBuilder.AlterColumn<string>(
                name: "source_storage_key",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "source_size_bytes",
                table: "candidate",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "source_sha256",
                table: "candidate",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_original_filename",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "candidate_source_storage_key_key",
                table: "candidate",
                column: "source_storage_key",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_original_filename_check",
                table: "candidate",
                sql: "btrim(source_original_filename) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_sha256_check",
                table: "candidate",
                sql: "octet_length(source_sha256) = 32");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_size_bytes_check",
                table: "candidate",
                sql: "source_size_bytes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_source_storage_key_check",
                table: "candidate",
                sql: "btrim(source_storage_key) <> ''");
        }
    }
}
