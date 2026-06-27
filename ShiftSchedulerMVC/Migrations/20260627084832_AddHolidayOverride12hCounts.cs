using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftSchedulerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayOverride12hCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Day12Count",
                table: "HolidayOverrides",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Night12Count",
                table: "HolidayOverrides",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Day12Count",
                table: "HolidayOverrides");

            migrationBuilder.DropColumn(
                name: "Night12Count",
                table: "HolidayOverrides");
        }
    }
}
