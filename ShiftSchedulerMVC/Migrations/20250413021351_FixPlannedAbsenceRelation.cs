using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftSchedulerMVC.Migrations
{
    /// <inheritdoc />
    public partial class FixPlannedAbsenceRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedAbsences_AspNetUsers_EmployeeId",
                table: "PlannedAbsences");

            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "PlannedAbsences",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedAbsences_AspNetUsers_EmployeeId",
                table: "PlannedAbsences",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedAbsences_AspNetUsers_EmployeeId",
                table: "PlannedAbsences");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "PlannedAbsences",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedAbsences_AspNetUsers_EmployeeId",
                table: "PlannedAbsences",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
