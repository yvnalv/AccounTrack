using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.MasterData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PriceListsBasePriceAndDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceLists_TenantId_CompanyId_Type_IsDefault",
                schema: "masterdata",
                table: "PriceLists");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                schema: "masterdata",
                table: "PriceLists");

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                schema: "masterdata",
                table: "Products",
                type: "numeric(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                schema: "masterdata",
                table: "Products",
                type: "numeric(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                schema: "masterdata",
                table: "PriceLists",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_CompanyId_Type",
                schema: "masterdata",
                table: "PriceLists",
                columns: new[] { "TenantId", "CompanyId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceLists_TenantId_CompanyId_Type",
                schema: "masterdata",
                table: "PriceLists");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                schema: "masterdata",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                schema: "masterdata",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "masterdata",
                table: "PriceLists");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                schema: "masterdata",
                table: "PriceLists",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_CompanyId_Type_IsDefault",
                schema: "masterdata",
                table: "PriceLists",
                columns: new[] { "TenantId", "CompanyId", "Type", "IsDefault" });
        }
    }
}
