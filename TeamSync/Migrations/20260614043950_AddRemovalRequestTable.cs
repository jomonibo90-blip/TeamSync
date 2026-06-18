using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class AddRemovalRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemovalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupMemberId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemovalRequests_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RemovalRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_RemovalRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_RemovalRequests_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemovalRequests_GroupMembers_GroupMemberId",
                        column: x => x.GroupMemberId,
                        principalTable: "GroupMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_ApprovedByUserId",
                table: "RemovalRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_GroupId",
                table: "RemovalRequests",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_GroupMemberId",
                table: "RemovalRequests",
                column: "GroupMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_RequestedByUserId",
                table: "RemovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_Status",
                table: "RemovalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RemovalRequests_UserId",
                table: "RemovalRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemovalRequests");
        }
    }
}
