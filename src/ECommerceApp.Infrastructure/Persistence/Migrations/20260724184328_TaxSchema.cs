using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaxSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TaxCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_CountryCode_RegionCode_TaxCategory",
                table: "TaxRates",
                columns: new[] { "CountryCode", "RegionCode", "TaxCategory" },
                unique: true,
                filter: "[RegionCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_CountryCode_TaxCategory",
                table: "TaxRates",
                columns: new[] { "CountryCode", "TaxCategory" },
                unique: true,
                filter: "[RegionCode] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxRates");
        }
    }
}
