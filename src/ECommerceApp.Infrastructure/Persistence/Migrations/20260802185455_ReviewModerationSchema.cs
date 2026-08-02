using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewModerationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<int>(type: "int", nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewReports_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReviewId_ReporterUserId",
                table: "ReviewReports",
                columns: new[] { "ReviewId", "ReporterUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewReports");
        }
    }
}
