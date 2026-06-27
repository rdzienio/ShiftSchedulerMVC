using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftSchedulerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftTimeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftType = table.Column<int>(type: "INTEGER", nullable: false),
                    StartHour = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTimeSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTimeSettings_ShiftType",
                table: "ShiftTimeSettings",
                column: "ShiftType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftTimeSettings");
        }
    }
}
