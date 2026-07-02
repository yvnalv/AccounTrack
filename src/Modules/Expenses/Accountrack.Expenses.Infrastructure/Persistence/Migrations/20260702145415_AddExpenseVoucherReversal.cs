using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Expenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseVoucherReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalEntryId",
                schema: "expenses",
                table: "ExpenseVouchers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                schema: "expenses",
                table: "ExpenseVouchers");
        }
    }
}
