using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Expenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Vouchers created before approval existed were always posted on creation — backfill them
            // to Posted (2) rather than the new-column default of Draft (0).
            migrationBuilder.Sql(
                "UPDATE [expenses].[ExpenseVouchers] SET [Status] = 2 WHERE [JournalEntryId] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "expenses",
                table: "ExpenseVouchers");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "expenses",
                table: "ExpenseVouchers");
        }
    }
}
