using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Smtp_Sign_In_Method_And_Grant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protected_grant",
                table: "smtp_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sign_in_method",
                table: "smtp_settings",
                type: "text",
                nullable: false,
                // Existing rows are App Password accounts (issue 03, decision 28).
                defaultValue: "app-password");

            migrationBuilder.AddCheckConstraint(
                name: "smtp_settings_sign_in_method_check",
                table: "smtp_settings",
                sql: "sign_in_method IN ('app-password', 'microsoft-account')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "smtp_settings_sign_in_method_check",
                table: "smtp_settings");

            migrationBuilder.DropColumn(
                name: "protected_grant",
                table: "smtp_settings");

            migrationBuilder.DropColumn(
                name: "sign_in_method",
                table: "smtp_settings");
        }
    }
}
