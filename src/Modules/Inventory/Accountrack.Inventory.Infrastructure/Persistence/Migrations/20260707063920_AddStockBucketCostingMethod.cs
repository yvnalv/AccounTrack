using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockBucketCostingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostingMethod",
                schema: "inventory",
                table: "StockCostBuckets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostingMethod",
                schema: "inventory",
                table: "StockCostBuckets");
        }
    }
}
