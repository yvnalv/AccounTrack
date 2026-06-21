using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.CompanyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsVatRegisteredToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVatRegistered",
                schema: "company",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVatRegistered",
                schema: "company",
                table: "Companies");
        }
    }
}
