using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Form_Layout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "form_layout",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vacancy_id = table.Column<long>(type: "bigint", nullable: false),
                    header_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    name_column_ordinal = table.Column<int>(type: "integer", nullable: true),
                    contact_email_column_ordinal = table.Column<int>(type: "integer", nullable: true),
                    contact_phone_column_ordinal = table.Column<int>(type: "integer", nullable: true),
                    cv_link_column_ordinal = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_layout", x => x.id);
                    table.CheckConstraint("form_layout_header_snapshot_check", "jsonb_array_length(header_snapshot) > 0");
                    table.CheckConstraint("form_layout_vacancy_id_check", "vacancy_id > 0");
                    table.ForeignKey(
                        name: "FK_form_layout_vacancy_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "form_layout_vacancy_key",
                table: "form_layout",
                column: "vacancy_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_layout");
        }
    }
}
