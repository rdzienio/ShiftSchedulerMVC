using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftSchedulerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalizedSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinalizedSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ManagerId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Shift = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalizedSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalizedSchedules_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinalizedSchedules_AspNetUsers_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinalizedSchedules_EmployeeId",
                table: "FinalizedSchedules",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalizedSchedules_ManagerId",
                table: "FinalizedSchedules",
                column: "ManagerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinalizedSchedules");
        }
    }
}
