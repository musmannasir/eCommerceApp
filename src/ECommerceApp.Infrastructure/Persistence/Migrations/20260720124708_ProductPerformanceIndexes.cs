using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_IsPublished",
                table: "Products",
                columns: new[] { "IsActive", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_PublishedAtUtc",
                table: "Products",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellingPrice",
                table: "Products",
                column: "SellingPrice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_IsPublished",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_PublishedAtUtc",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellingPrice",
                table: "Products");
        }
    }
}
