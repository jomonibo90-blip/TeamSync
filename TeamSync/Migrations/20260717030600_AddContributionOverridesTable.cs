using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSync.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionOverridesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsStudentSubmitted column to Contributions table
            migrationBuilder.AddColumn<bool>(
                name: "IsStudentSubmitted",
                table: "Contributions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Create ContributionOverrides table
            migrationBuilder.CreateTable(
                name: "ContributionOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContributionId = table.Column<int>(type: "int", nullable: false),
                    OverriddenById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OverriddenAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    OriginalHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OriginalDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    DisputeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisputedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DisputedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContributionOverrides_AspNetUsers_DisputedById",
                        column: x => x.DisputedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContributionOverrides_AspNetUsers_OverriddenById",
                        column: x => x.OverriddenById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ContributionOverrides_Contributions_ContributionId",
                        column: x => x.ContributionId,
                        principalTable: "Contributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_ContributionOverrides_ContributionId",
                table: "ContributionOverrides",
                column: "ContributionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionOverrides_DisputedById",
                table: "ContributionOverrides",
                column: "DisputedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionOverrides_OverriddenById",
                table: "ContributionOverrides",
                column: "OverriddenById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContributionOverrides");

            migrationBuilder.DropColumn(
                name: "IsStudentSubmitted",
                table: "Contributions");
        }
    }
}

