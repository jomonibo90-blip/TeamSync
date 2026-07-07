using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    public partial class MakeTaskGroupNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear sentinel zeros so we can make the FK nullable safely
            migrationBuilder.Sql("UPDATE Tasks SET GroupId = NULL WHERE GroupId = 0;");

            // Drop existing FK to Groups
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Groups_GroupId",
                table: "Tasks");

            // Alter column to be nullable
            migrationBuilder.AlterColumn<int>(
                name: "GroupId",
                table: "Tasks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Re-create FK with ON DELETE SET NULL
            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Groups_GroupId",
                table: "Tasks",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Before reverting to non-nullable, set NULLs back to 0 (legacy sentinel)
            migrationBuilder.Sql("UPDATE Tasks SET GroupId = 0 WHERE GroupId IS NULL;");

            // Drop FK
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Groups_GroupId",
                table: "Tasks");

            // Alter column back to non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "GroupId",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Re-create original FK with cascade delete
            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Groups_GroupId",
                table: "Tasks",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
