using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class AddAddMemberRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_RequestedByUserId",
                table: "RemovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_UserId",
                table: "RemovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_GroupMembers_GroupMemberId",
                table: "RemovalRequests");

            migrationBuilder.CreateTable(
                name: "AddMemberRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddMemberRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AddMemberRequests_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AddMemberRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AddMemberRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AddMemberRequests_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddMemberRequests_ApprovedByUserId",
                table: "AddMemberRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AddMemberRequests_GroupId",
                table: "AddMemberRequests",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AddMemberRequests_RequestedByUserId",
                table: "AddMemberRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AddMemberRequests_UserId",
                table: "AddMemberRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_RequestedByUserId",
                table: "RemovalRequests",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_UserId",
                table: "RemovalRequests",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_GroupMembers_GroupMemberId",
                table: "RemovalRequests",
                column: "GroupMemberId",
                principalTable: "GroupMembers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_RequestedByUserId",
                table: "RemovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_UserId",
                table: "RemovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RemovalRequests_GroupMembers_GroupMemberId",
                table: "RemovalRequests");

            migrationBuilder.DropTable(
                name: "AddMemberRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_RequestedByUserId",
                table: "RemovalRequests",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_AspNetUsers_UserId",
                table: "RemovalRequests",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RemovalRequests_GroupMembers_GroupMemberId",
                table: "RemovalRequests",
                column: "GroupMemberId",
                principalTable: "GroupMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
