using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forjix.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialCategories", x => x.Id);
                    table.CheckConstraint("CK_FinancialCategory_Type", "[Type] IN (0,1)");
                });

            migrationBuilder.CreateTable(
                name: "AccountsPayable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    TotalInstallments = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountsPayable", x => x.Id);
                    table.CheckConstraint("CK_Payable_Amounts", "[OriginalAmount] > 0 AND [OpenAmount] >= 0 AND [OpenAmount] <= [OriginalAmount]");
                    table.CheckConstraint("CK_Payable_Dates", "[DueDate] >= [IssueDate]");
                    table.CheckConstraint("CK_Payable_Installments", "[InstallmentNumber] >= 1 AND [InstallmentNumber] <= [TotalInstallments] AND [TotalInstallments] <= 120");
                    table.CheckConstraint("CK_Payable_Status", "[Status] IN (0,1,2,4)");
                    table.ForeignKey(
                        name: "FK_AccountsPayable_FinancialCategories_FinancialCategoryId",
                        column: x => x.FinancialCategoryId,
                        principalTable: "FinancialCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsPayable_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsPayable_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsPayable_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsPayable_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountsReceivable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    TotalInstallments = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountsReceivable", x => x.Id);
                    table.CheckConstraint("CK_Receivable_Amounts", "[OriginalAmount] > 0 AND [OpenAmount] >= 0 AND [OpenAmount] <= [OriginalAmount]");
                    table.CheckConstraint("CK_Receivable_Dates", "[DueDate] >= [IssueDate]");
                    table.CheckConstraint("CK_Receivable_Installments", "[InstallmentNumber] >= 1 AND [InstallmentNumber] <= [TotalInstallments] AND [TotalInstallments] <= 120");
                    table.CheckConstraint("CK_Receivable_Status", "[Status] IN (0,1,2,4)");
                    table.ForeignKey(
                        name: "FK_AccountsReceivable_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsReceivable_FinancialCategories_FinancialCategoryId",
                        column: x => x.FinancialCategoryId,
                        principalTable: "FinancialCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsReceivable_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsReceivable_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountsReceivable_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountReceivableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountPayableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Penalty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CashMovementId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalCashMovementId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReversedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialPayments", x => x.Id);
                    table.CheckConstraint("CK_Payment_Account", "([Type] = 0 AND [AccountReceivableId] IS NOT NULL AND [AccountPayableId] IS NULL) OR ([Type] = 1 AND [AccountPayableId] IS NOT NULL AND [AccountReceivableId] IS NULL)");
                    table.CheckConstraint("CK_Payment_Amounts", "[Amount] > 0 AND [PrincipalAmount] > 0 AND [Discount] >= 0 AND [Interest] >= 0 AND [Penalty] >= 0 AND [PrincipalAmount] = [Amount] + [Discount] - [Interest] - [Penalty]");
                    table.CheckConstraint("CK_Payment_Method", "[PaymentMethod] IN (0,1,2,3)");
                    table.ForeignKey(
                        name: "FK_FinancialPayments_AccountsPayable_AccountPayableId",
                        column: x => x.AccountPayableId,
                        principalTable: "AccountsPayable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPayments_AccountsReceivable_AccountReceivableId",
                        column: x => x.AccountReceivableId,
                        principalTable: "AccountsReceivable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPayments_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPayments_CashMovements_ReversalCashMovementId",
                        column: x => x.ReversalCashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPayments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPayments_Users_ReversedByUserId",
                        column: x => x.ReversedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_CreatedByUserId",
                table: "AccountsPayable",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_DueDate",
                table: "AccountsPayable",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_FinancialCategoryId",
                table: "AccountsPayable",
                column: "FinancialCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_GroupId_InstallmentNumber",
                table: "AccountsPayable",
                columns: new[] { "GroupId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_PurchaseId_InstallmentNumber",
                table: "AccountsPayable",
                columns: new[] { "PurchaseId", "InstallmentNumber" },
                unique: true,
                filter: "[PurchaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_Status_DueDate",
                table: "AccountsPayable",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_SupplierId",
                table: "AccountsPayable",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsPayable_UpdatedByUserId",
                table: "AccountsPayable",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_CreatedByUserId",
                table: "AccountsReceivable",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_CustomerId",
                table: "AccountsReceivable",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_DueDate",
                table: "AccountsReceivable",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_FinancialCategoryId",
                table: "AccountsReceivable",
                column: "FinancialCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_GroupId_InstallmentNumber",
                table: "AccountsReceivable",
                columns: new[] { "GroupId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_SaleId_InstallmentNumber",
                table: "AccountsReceivable",
                columns: new[] { "SaleId", "InstallmentNumber" },
                unique: true,
                filter: "[SaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_Status_DueDate",
                table: "AccountsReceivable",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountsReceivable_UpdatedByUserId",
                table: "AccountsReceivable",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialCategories_Type_Name",
                table: "FinancialCategories",
                columns: new[] { "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_AccountPayableId",
                table: "FinancialPayments",
                column: "AccountPayableId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_AccountReceivableId",
                table: "FinancialPayments",
                column: "AccountReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_CashMovementId",
                table: "FinancialPayments",
                column: "CashMovementId",
                unique: true,
                filter: "[CashMovementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_CreatedByUserId",
                table: "FinancialPayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_IdempotencyKey",
                table: "FinancialPayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_PaymentDate_Type",
                table: "FinancialPayments",
                columns: new[] { "PaymentDate", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_ReversalCashMovementId",
                table: "FinancialPayments",
                column: "ReversalCashMovementId",
                unique: true,
                filter: "[ReversalCashMovementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPayments_ReversedByUserId",
                table: "FinancialPayments",
                column: "ReversedByUserId");
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.categories.view')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000001', N'financial.categories.view', N'Visualizar categorias financeiras', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.categories.manage')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000002', N'financial.categories.manage', N'Gerenciar categorias financeiras', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.receivable.view')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000003', N'financial.receivable.view', N'Visualizar contas a receber', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.receivable.manage')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000004', N'financial.receivable.manage', N'Gerenciar contas a receber', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.receivable.pay')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000005', N'financial.receivable.pay', N'Baixar e estornar contas a receber', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.payable.view')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000006', N'financial.payable.view', N'Visualizar contas a pagar', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.payable.manage')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000007', N'financial.payable.manage', N'Gerenciar contas a pagar', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.payable.pay')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000008', N'financial.payable.pay', N'Baixar e estornar contas a pagar', N'Financeiro');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'financial.dashboard.view')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module])
                    VALUES ('9f000000-0000-0000-0000-000000000009', N'financial.dashboard.view', N'Visualizar dashboard financeiro', N'Financeiro');
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt])
                SELECT r.[Id], p.[Id], SYSUTCDATETIME()
                FROM [Roles] r CROSS JOIN [Permissions] p
                WHERE r.[IsSystem] = 1 AND r.[NormalizedName] = N'ADMINISTRADOR'
                  AND p.[Code] IN (N'financial.categories.view', N'financial.categories.manage', N'financial.receivable.view', N'financial.receivable.manage', N'financial.receivable.pay', N'financial.payable.view', N'financial.payable.manage', N'financial.payable.pay', N'financial.dashboard.view')
                  AND NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp FROM [RolePermissions] rp JOIN [Permissions] p ON rp.[PermissionId] = p.[Id] WHERE p.[Code] IN (N'financial.categories.view', N'financial.categories.manage', N'financial.receivable.view', N'financial.receivable.manage', N'financial.receivable.pay', N'financial.payable.view', N'financial.payable.manage', N'financial.payable.pay', N'financial.dashboard.view');
                DELETE FROM [Permissions] WHERE [Code] IN (N'financial.categories.view', N'financial.categories.manage', N'financial.receivable.view', N'financial.receivable.manage', N'financial.receivable.pay', N'financial.payable.view', N'financial.payable.manage', N'financial.payable.pay', N'financial.dashboard.view');
                """);
            migrationBuilder.DropTable(
                name: "FinancialPayments");

            migrationBuilder.DropTable(
                name: "AccountsPayable");

            migrationBuilder.DropTable(
                name: "AccountsReceivable");

            migrationBuilder.DropTable(
                name: "FinancialCategories");
        }
    }
}
