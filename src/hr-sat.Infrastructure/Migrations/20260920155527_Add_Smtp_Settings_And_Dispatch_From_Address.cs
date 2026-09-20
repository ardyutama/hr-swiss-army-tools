using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Smtp_Settings_And_Dispatch_From_Address : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "from_address",
                table: "dispatch",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "smtp_settings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    host = table.Column<string>(type: "text", nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    protected_password = table.Column<string>(type: "text", nullable: true),
                    from_address = table.Column<string>(type: "text", nullable: false),
                    from_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smtp_settings", x => x.id);
                    table.CheckConstraint("smtp_settings_singleton_check", "id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "smtp_settings");

            migrationBuilder.DropColumn(
                name: "from_address",
                table: "dispatch");
        }
    }
}
