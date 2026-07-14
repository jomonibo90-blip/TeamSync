using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContributionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "HoursSpent",
                table: "Contributions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Contributions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Contributions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedAt",
                table: "Contributions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RecordedById",
                table: "Contributions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Contributions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_RecordedById",
                table: "Contributions",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_TaskId_UserId",
                table: "Contributions",
                columns: new[] { "TaskId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions",
                column: "RecordedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_RecordedById",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_TaskId_UserId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "RecordedById",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Contributions");

            migrationBuilder.AlterColumn<int>(
                name: "HoursSpent",
                table: "Contributions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Contributions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);
        }
    }
}
