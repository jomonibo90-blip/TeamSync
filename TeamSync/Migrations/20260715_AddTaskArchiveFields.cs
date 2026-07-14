using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Tasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedById",
                table: "Tasks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ArchivedById",
                table: "Tasks",
                column: "ArchivedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_AspNetUsers_ArchivedById",
                table: "Tasks",
                column: "ArchivedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_AspNetUsers_ArchivedById",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ArchivedById",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchivedById",
                table: "Tasks");
        }
    }
}
