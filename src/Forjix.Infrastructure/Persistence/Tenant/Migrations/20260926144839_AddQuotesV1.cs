using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forjix.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.view')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000001', N'quotes.view', N'Visualizar orçamentos', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.create')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000002', N'quotes.create', N'Criar orçamentos', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.edit')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000003', N'quotes.edit', N'Editar orçamentos', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.status')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000004', N'quotes.status', N'Enviar, aprovar e rejeitar orçamentos', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.cancel')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000005', N'quotes.cancel', N'Cancelar orçamentos', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.convert')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000006', N'quotes.convert', N'Converter orçamentos em vendas', N'Comercial');
                IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'quotes.print')
                    INSERT INTO [Permissions] ([Id], [Code], [Name], [Module]) VALUES ('8a000000-0000-0000-0000-000000000007', N'quotes.print', N'Imprimir orçamentos', N'Comercial');
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt])
                SELECT r.[Id], p.[Id], SYSUTCDATETIME() FROM [Roles] r CROSS JOIN [Permissions] p
                WHERE r.[IsSystem] = 1 AND r.[NormalizedName] = N'ADMINISTRADOR' AND p.[Code] IN (N'quotes.view', N'quotes.create', N'quotes.edit', N'quotes.status', N'quotes.cancel', N'quotes.convert', N'quotes.print')
                AND NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
                """);
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.CheckConstraint("CK_Quote_Amounts", "[Subtotal] >= 0 AND [Discount] >= 0 AND [Total] >= 0 AND [Total] <= [Subtotal]");
                    table.CheckConstraint("CK_Quote_Conversion", "([Status] = 5 AND [SaleId] IS NOT NULL AND [ConvertedAt] IS NOT NULL) OR ([Status] <> 5 AND [SaleId] IS NULL AND [ConvertedAt] IS NULL)");
                    table.CheckConstraint("CK_Quote_Dates", "[ValidUntil] >= [IssueDate]");
                    table.CheckConstraint("CK_Quote_Status", "[Status] IN (0,1,2,3,5,6)");
                    table.ForeignKey(
                        name: "FK_Quotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteSequences", x => x.Id);
                    table.CheckConstraint("CK_QuoteSequence", "[Id] = 1 AND [LastValue] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "QuoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteItems", x => x.Id);
                    table.CheckConstraint("CK_QuoteItem_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [Discount] >= 0 AND [Total] >= 0 AND [Total] = ROUND([Quantity] * [UnitPrice], 2) - [Discount]");
                    table.ForeignKey(
                        name: "FK_QuoteItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteItems_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_ProductId",
                table: "QuoteItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_QuoteId_ProductId",
                table: "QuoteItems",
                columns: new[] { "QuoteId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_IssueDate",
                table: "Quotes",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Number",
                table: "Quotes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SaleId",
                table: "Quotes",
                column: "SaleId",
                unique: true,
                filter: "[SaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status_ValidUntil",
                table: "Quotes",
                columns: new[] { "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_UpdatedByUserId",
                table: "Quotes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_UserId",
                table: "Quotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ValidUntil",
                table: "Quotes",
                column: "ValidUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp FROM [RolePermissions] rp JOIN [Permissions] p ON p.[Id] = rp.[PermissionId] WHERE p.[Code] IN (N'quotes.view', N'quotes.create', N'quotes.edit', N'quotes.status', N'quotes.cancel', N'quotes.convert', N'quotes.print');
                DELETE FROM [Permissions] WHERE [Code] IN (N'quotes.view', N'quotes.create', N'quotes.edit', N'quotes.status', N'quotes.cancel', N'quotes.convert', N'quotes.print');
                """);
            migrationBuilder.DropTable(
                name: "QuoteItems");

            migrationBuilder.DropTable(
                name: "QuoteSequences");

            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
