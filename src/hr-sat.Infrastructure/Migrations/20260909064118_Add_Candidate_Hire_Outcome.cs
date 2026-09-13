using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Candidate_Hire_Outcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hire_outcome",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddCheckConstraint(
                name: "candidate_hire_outcome_check",
                table: "candidate",
                sql: "hire_outcome IN ('none', 'hired', 'runaway', 'declined')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "candidate_hire_outcome_check",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "hire_outcome",
                table: "candidate");
        }
    }
}
