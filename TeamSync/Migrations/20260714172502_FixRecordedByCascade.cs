using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class FixRecordedByCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions",
                column: "RecordedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_AspNetUsers_RecordedById",
                table: "Contributions",
                column: "RecordedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
