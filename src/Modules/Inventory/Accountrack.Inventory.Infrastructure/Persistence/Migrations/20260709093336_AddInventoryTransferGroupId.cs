using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTransferGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransferGroupId",
                schema: "inventory",
                table: "InventoryTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_CompanyId_TransferGroupId",
                schema: "inventory",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "CompanyId", "TransferGroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_TenantId_CompanyId_TransferGroupId",
                schema: "inventory",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TransferGroupId",
                schema: "inventory",
                table: "InventoryTransactions");
        }
    }
}
