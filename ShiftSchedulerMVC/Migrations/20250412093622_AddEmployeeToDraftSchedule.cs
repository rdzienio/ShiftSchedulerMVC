using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftSchedulerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeToDraftSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DraftSchedules_EmployeeId",
                table: "DraftSchedules",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DraftSchedules_AspNetUsers_EmployeeId",
                table: "DraftSchedules",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftSchedules_AspNetUsers_EmployeeId",
                table: "DraftSchedules");

            migrationBuilder.DropIndex(
                name: "IX_DraftSchedules_EmployeeId",
                table: "DraftSchedules");
        }
    }
}
