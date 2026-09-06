using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Vacancy_Needed_Hires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "needed_hires",
                table: "vacancy",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "vacancy_needed_hires_check",
                table: "vacancy",
                sql: "needed_hires IS NULL OR needed_hires BETWEEN 1 AND 9999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "vacancy_needed_hires_check",
                table: "vacancy");

            migrationBuilder.DropColumn(
                name: "needed_hires",
                table: "vacancy");
        }
    }
}
