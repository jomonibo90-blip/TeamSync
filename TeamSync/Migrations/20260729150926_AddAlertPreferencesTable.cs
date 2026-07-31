using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertPreferencesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NotificationFrequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Weekly"),
                    DigestDayOfWeek = table.Column<int>(type: "int", nullable: true, defaultValue: 1),
                    DigestHourUtc = table.Column<int>(type: "int", nullable: true, defaultValue: 9),
                    ReceiveTaskAssignmentAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReceiveApprovalRejectionAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReceiveStatusChangeAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReceiveCommentAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReceiveGroupAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDigestSentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertPreferences_UserId",
                table: "AlertPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertPreferences");
        }
    }
}
