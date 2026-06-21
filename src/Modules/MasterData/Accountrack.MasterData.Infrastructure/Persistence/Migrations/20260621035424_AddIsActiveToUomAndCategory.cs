using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.MasterData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToUomAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "masterdata",
                table: "UnitsOfMeasure",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "masterdata",
                table: "ProductCategories",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "masterdata",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "masterdata",
                table: "ProductCategories");
        }
    }
}
