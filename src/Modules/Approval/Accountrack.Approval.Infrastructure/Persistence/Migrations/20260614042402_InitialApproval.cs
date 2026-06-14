using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accountrack.Approval.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approval");

            migrationBuilder.CreateTable(
                name: "ApprovalDefinitions",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentLevel = table.Column<int>(type: "int", nullable: false),
                    MaxLevel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalConditions",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attribute = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalConditions_ApprovalDefinitions_ApprovalDefinitionId",
                        column: x => x.ApprovalDefinitionId,
                        principalSchema: "approval",
                        principalTable: "ApprovalDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ApproverType = table.Column<int>(type: "int", nullable: false),
                    ApproverRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalDefinitions_ApprovalDefinitionId",
                        column: x => x.ApprovalDefinitionId,
                        principalSchema: "approval",
                        principalTable: "ApprovalDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalActions",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ActedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalActions_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "approval",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequestSteps",
                schema: "approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ApproverType = table.Column<int>(type: "int", nullable: false),
                    ApproverRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequestSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSteps_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "approval",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalActions_ApprovalRequestId",
                schema: "approval",
                table: "ApprovalActions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalConditions_ApprovalDefinitionId",
                schema: "approval",
                table: "ApprovalConditions",
                column: "ApprovalDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDefinitions_TenantId_CompanyId_DocumentType",
                schema: "approval",
                table: "ApprovalDefinitions",
                columns: new[] { "TenantId", "CompanyId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TenantId_CompanyId_DocumentType_DocumentId",
                schema: "approval",
                table: "ApprovalRequests",
                columns: new[] { "TenantId", "CompanyId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TenantId_CompanyId_Status",
                schema: "approval",
                table: "ApprovalRequests",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSteps_ApprovalRequestId",
                schema: "approval",
                table: "ApprovalRequestSteps",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalDefinitionId",
                schema: "approval",
                table: "ApprovalSteps",
                column: "ApprovalDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalActions",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "ApprovalConditions",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "ApprovalRequestSteps",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "ApprovalRequests",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "ApprovalDefinitions",
                schema: "approval");
        }
    }
}
