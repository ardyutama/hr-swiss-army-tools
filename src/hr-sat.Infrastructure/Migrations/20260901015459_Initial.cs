using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacancy",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancy", x => x.id);
                    table.CheckConstraint("vacancy_closed_at_check", "(status = 'open' AND closed_at IS NULL) OR (status = 'closed' AND closed_at IS NOT NULL)");
                    table.CheckConstraint("vacancy_status_check", "status IN ('open', 'closed')");
                    table.CheckConstraint("vacancy_title_check", "char_length(btrim(title)) BETWEEN 1 AND 200");
                });

            migrationBuilder.CreateTable(
                name: "candidate",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    review_status = table.Column<string>(type: "text", nullable: false, defaultValue: "new"),
                    extraction_status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    full_name = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    source_sender_name = table.Column<string>(type: "text", nullable: true),
                    source_sender_email = table.Column<string>(type: "text", nullable: true),
                    source_subject = table.Column<string>(type: "text", nullable: true),
                    source_body_text = table.Column<string>(type: "text", nullable: true),
                    source_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_original_filename = table.Column<string>(type: "text", nullable: false),
                    source_storage_key = table.Column<string>(type: "text", nullable: false),
                    source_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    source_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate", x => x.id);
                    table.CheckConstraint("candidate_contact_email_check", "contact_email IS NULL OR char_length(btrim(contact_email)) BETWEEN 1 AND 320");
                    table.CheckConstraint("candidate_contact_phone_check", "contact_phone IS NULL OR char_length(btrim(contact_phone)) BETWEEN 1 AND 100");
                    table.CheckConstraint("candidate_extraction_status_check", "extraction_status IN ('pending', 'succeeded', 'failed')");
                    table.CheckConstraint("candidate_full_name_check", "full_name IS NULL OR char_length(btrim(full_name)) BETWEEN 1 AND 300");
                    table.CheckConstraint("candidate_review_status_check", "review_status IN ('new', 'flagged', 'shortlisted', 'rejected')");
                    table.CheckConstraint("candidate_source_original_filename_check", "btrim(source_original_filename) <> ''");
                    table.CheckConstraint("candidate_source_sender_email_check", "source_sender_email IS NULL OR char_length(btrim(source_sender_email)) BETWEEN 1 AND 320");
                    table.CheckConstraint("candidate_source_sender_name_check", "source_sender_name IS NULL OR char_length(btrim(source_sender_name)) BETWEEN 1 AND 300");
                    table.CheckConstraint("candidate_source_sha256_check", "octet_length(source_sha256) = 32");
                    table.CheckConstraint("candidate_source_size_bytes_check", "source_size_bytes > 0");
                    table.CheckConstraint("candidate_source_storage_key_check", "btrim(source_storage_key) <> ''");
                    table.ForeignKey(
                        name: "FK_candidate_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_requirement",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    phrase = table.Column<string>(type: "text", nullable: false),
                    phrase_normalized = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(btrim(phrase))", stored: true),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancy_requirement", x => x.id);
                    table.CheckConstraint("vacancy_requirement_phrase_check", "char_length(btrim(phrase)) BETWEEN 1 AND 200");
                    table.CheckConstraint("vacancy_requirement_position_check", "position >= 1");
                    table.ForeignKey(
                        name: "FK_vacancy_requirement_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cv_document",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    candidate_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    original_filename = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_document", x => x.id);
                    table.CheckConstraint("cv_document_original_filename_check", "btrim(original_filename) <> ''");
                    table.CheckConstraint("cv_document_position_check", "position >= 1");
                    table.CheckConstraint("cv_document_sha256_check", "octet_length(sha256) = 32");
                    table.CheckConstraint("cv_document_size_bytes_check", "size_bytes > 0");
                    table.CheckConstraint("cv_document_storage_key_check", "btrim(storage_key) <> ''");
                    table.ForeignKey(
                        name: "FK_cv_document_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "candidate_source_storage_key_key",
                table: "candidate",
                column: "source_storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "candidate_vacancy_imported_idx",
                table: "candidate",
                columns: new[] { "vacancy_id", "imported_at", "id" });

            migrationBuilder.CreateIndex(
                name: "candidate_vacancy_source_sha256_key",
                table: "candidate",
                columns: new[] { "vacancy_id", "source_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "cv_document_candidate_position_key",
                table: "cv_document",
                columns: new[] { "candidate_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "cv_document_candidate_primary_idx",
                table: "cv_document",
                column: "candidate_id",
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "cv_document_storage_key_key",
                table: "cv_document",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "vacancy_requirement_vacancy_phrase_key",
                table: "vacancy_requirement",
                columns: new[] { "vacancy_id", "phrase_normalized" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE vacancy_requirement
                ADD CONSTRAINT vacancy_requirement_vacancy_position_key
                    UNIQUE (vacancy_id, position)
                    DEFERRABLE INITIALLY IMMEDIATE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_document");

            migrationBuilder.DropTable(
                name: "vacancy_requirement");

            migrationBuilder.DropTable(
                name: "candidate");

            migrationBuilder.DropTable(
                name: "vacancy");
        }
    }
}
