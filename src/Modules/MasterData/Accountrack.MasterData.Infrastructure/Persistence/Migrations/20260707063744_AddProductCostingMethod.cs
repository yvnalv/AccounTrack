using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.MasterData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCostingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostingMethod",
                schema: "masterdata",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostingMethod",
                schema: "masterdata",
                table: "Products");
        }
    }
}
