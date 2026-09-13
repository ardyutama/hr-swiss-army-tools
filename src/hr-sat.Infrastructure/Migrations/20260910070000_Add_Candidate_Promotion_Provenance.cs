using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hr_sat.Infrastructure.Migrations;

public partial class Add_Candidate_Promotion_Provenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "promoted_at",
            table: "candidate",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "promoted_from_round_number",
            table: "candidate",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "promoted_at",
            table: "candidate");

        migrationBuilder.DropColumn(
            name: "promoted_from_round_number",
            table: "candidate");
    }
}