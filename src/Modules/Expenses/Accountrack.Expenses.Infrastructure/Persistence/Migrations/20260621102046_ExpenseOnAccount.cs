using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Expenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseOnAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CashAccountId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ApOpenItemId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApOpenItemId",
                schema: "expenses",
                table: "ExpenseVouchers");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "expenses",
                table: "ExpenseVouchers");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "expenses",
                table: "ExpenseVouchers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashAccountId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
